using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CoiDataExporter.Contracts;
using Mafi.Base;
using Mafi.Core.Prototypes;
using Newtonsoft.Json;

namespace CoiDataExporter.Mod
{
    internal sealed class ExportPipeline
    {
        private readonly string _modRoot;
        private readonly Action<string> _info;
        private readonly Action<string> _warning;
        private readonly ReflectionReader _reader = new ReflectionReader();

        public ExportPipeline(
            string modRoot,
            Action<string> info,
            Action<string> warning)
        {
            _modRoot = string.IsNullOrWhiteSpace(modRoot)
                ? GetDefaultModRoot()
                : modRoot;
            _info = info ?? delegate { };
            _warning = warning ?? delegate { };
        }

        public void Run(ProtosDb protosDb)
        {
            if (protosDb == null)
            {
                throw new ArgumentNullException(nameof(protosDb));
            }

            var document = new ExportDocument
            {
                SchemaVersion = 1,
                GameVersion = GetGameVersion(),
                ExportedAtUtc = DateTime.UtcNow
            };

            var productPrototypes = ReadAll(protosDb,
                "Mafi.Core.Products.ProductProto");
            var recipePrototypes = ReadAll(protosDb,
                "Mafi.Core.Factory.Recipes.RecipeProto");
            var buildingPrototypes = ReadAll(protosDb,
                "Mafi.Core.Entities.Static.StaticEntityProto");

            _info(string.Format(
                CultureInfo.InvariantCulture,
                "Prototype counts: products={0}, recipes={1}, static_entities={2}",
                productPrototypes.Count,
                recipePrototypes.Count,
                buildingPrototypes.Count));

            foreach (var product in productPrototypes)
            {
                AddProduct(document, product);
            }

            foreach (var recipe in recipePrototypes)
            {
                AddRecipe(document, recipe);
            }

            foreach (var building in buildingPrototypes)
            {
                AddBuilding(document, building);
            }

            if (document.Products.Count == 0 &&
                document.Recipes.Count == 0 &&
                document.Buildings.Count == 0)
            {
                throw new InvalidOperationException(
                    "The prototype database returned no exportable data. " +
                    "The game API may have changed or the export ran too early.");
            }

            document.Products = document.Products
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList();
            document.Recipes = document.Recipes
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList();
            document.Buildings = document.Buildings
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            var outputDirectory = Path.Combine(_modRoot, "export");
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, "coi-data.json");
            WriteJsonAtomically(document, outputPath);
            WriteConvenienceFiles(document, outputDirectory);
            WriteStatusFile(document, outputDirectory, outputPath);
        }

        private void AddProduct(ExportDocument document, object prototype)
        {
            var id = _reader.Id(prototype);
            if (string.IsNullOrWhiteSpace(id) || ContainsId(document.Products, id))
            {
                return;
            }

            var dto = new ProductDto
            {
                Id = id,
                Name = ReadName(prototype),
                State = _reader.Text(_reader.Read(prototype, "State")),
                IsStorable = _reader.Boolean(
                    _reader.Read(prototype, "IsStorable"), true),
                IsTrash = _reader.Boolean(
                    _reader.Read(prototype, "IsTrash")),
                Radioactivity = _reader.Number(
                    _reader.Read(prototype, "Radioactivity"))
            };

            document.Products.Add(dto);
        }

        private void AddRecipe(ExportDocument document, object prototype)
        {
            var id = _reader.Id(prototype);
            if (string.IsNullOrWhiteSpace(id) || ContainsId(document.Recipes, id))
            {
                return;
            }

            var dto = new RecipeDto
            {
                Id = id,
                Name = ReadName(prototype),
                DurationSeconds = _reader.Number(
                    _reader.Read(prototype, "Duration", "Seconds"))
            };

            AddQuantities(
                dto.Inputs,
                _reader.Read(prototype, "AllUserVisibleInputs"));
            AddQuantities(
                dto.Outputs,
                _reader.Read(prototype, "AllUserVisibleOutputs"));

            document.Recipes.Add(dto);
        }

        private void AddBuilding(ExportDocument document, object prototype)
        {
            var id = _reader.Id(prototype);
            if (string.IsNullOrWhiteSpace(id) || ContainsId(document.Buildings, id))
            {
                return;
            }

            var typeName = prototype.GetType().Name;
            var category = ReadLastCategory(prototype);
            var costs = _reader.Read(prototype, "Costs");
            var maintenance = _reader.Read(costs, "Maintenance");
            var maintenanceProduct = _reader.Read(maintenance, "Product");

            var dto = new BuildingDto
            {
                Id = id,
                Name = ReadName(prototype),
                CategoryId = category.Id,
                CategoryName = category.Name,
                IsFarm = typeName.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0,
                IsStorage = typeName.IndexOf("Storage", StringComparison.OrdinalIgnoreCase) >= 0,
                IsMine = typeName.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0,
                Workers = _reader.Integer(_reader.Read(costs, "Workers")),
                MaintenanceProductId = _reader.Id(maintenanceProduct),
                MaintenancePerMonth = _reader.Number(
                    _reader.Read(maintenance, "MaxMaintenancePerMonth")),
                ElectricityConsumed = _reader.Number(
                    _reader.Read(prototype, "ConsumedPowerPerTick")),
                ElectricityGenerated = ReadGeneratedElectricity(prototype),
                StorageCapacity = _reader.Number(
                    _reader.Read(prototype, "Capacity")),
                TransferSpeedPerMinute = ReadTransferSpeed(prototype),
                ResearchSpeed = ReadResearchSpeed(prototype)
            };

            AddQuantities(
                dto.BuildCosts,
                _reader.Read(costs, "Price", "Products"));

            foreach (var recipe in _reader.Items(_reader.Read(prototype, "Recipes")))
            {
                var recipeId = _reader.Id(recipe);
                if (!string.IsNullOrWhiteSpace(recipeId) &&
                    !dto.RecipeIds.Contains(recipeId, StringComparer.Ordinal))
                {
                    dto.RecipeIds.Add(recipeId);
                }
            }

            document.Buildings.Add(dto);
        }

        private double ReadGeneratedElectricity(object prototype)
        {
            var direct = _reader.Number(_reader.Read(prototype, "OutputElectricity"));
            if (direct != 0d)
            {
                return direct;
            }

            var recipeOutputs = new List<double>();
            foreach (var recipe in _reader.Items(_reader.Read(prototype, "Recipes")))
            {
                foreach (var output in _reader.Items(
                    _reader.Read(recipe, "AllUserVisibleOutputs")))
                {
                    recipeOutputs.Add(_reader.Number(_reader.Read(output, "Quantity")));
                }
            }

            return recipeOutputs.Count == 0 ? 0d : recipeOutputs.Max();
        }

        private double ReadTransferSpeed(object prototype)
        {
            var transferLimit = _reader.Number(
                _reader.Read(prototype, "TransferLimit"));
            var durationSeconds = _reader.Number(
                _reader.Read(prototype, "TransferLimitDuration", "Seconds"));

            if (durationSeconds <= 0d)
            {
                return 0d;
            }

            return transferLimit / durationSeconds * 60d;
        }

        private double ReadResearchSpeed(object prototype)
        {
            var durationSeconds = _reader.Number(
                _reader.Read(prototype, "DurationForRecipe", "Seconds"));
            var steps = _reader.Number(
                _reader.Read(prototype, "StepsPerRecipe"));

            return durationSeconds <= 0d ? 0d : 60d / durationSeconds * steps;
        }

        private void AddQuantities(
            ICollection<ProductQuantityDto> target,
            object source)
        {
            foreach (var item in _reader.Items(source))
            {
                var product = _reader.Read(item, "Product");
                var productId = _reader.Id(product);
                if (string.IsNullOrWhiteSpace(productId))
                {
                    _warning("Skipped a quantity with no product ID.");
                    continue;
                }

                target.Add(new ProductQuantityDto
                {
                    ProductId = productId,
                    ProductName = ReadName(product),
                    Amount = _reader.Number(_reader.Read(item, "Quantity"))
                });
            }
        }

        private string ReadName(object prototype)
        {
            var localized = _reader.Read(prototype, "Strings", "Name");
            var text = _reader.Text(localized);
            return string.IsNullOrWhiteSpace(text)
                ? _reader.Id(prototype)
                : text;
        }

        private CategoryData ReadLastCategory(object prototype)
        {
            var categories = _reader.Items(
                _reader.Read(prototype, "Graphics", "Categories"));
            var last = categories.LastOrDefault();
            if (last == null)
            {
                return new CategoryData();
            }

            return new CategoryData
            {
                Id = _reader.Id(last),
                Name = ReadName(last)
            };
        }

        private List<object> ReadAll(ProtosDb protosDb, string fullTypeName)
        {
            var prototypeType = ResolveType(fullTypeName);
            if (prototypeType == null)
            {
                _warning("Prototype type was not found: " + fullTypeName);
                return new List<object>();
            }

            var allMethod = typeof(ProtosDb)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "All")
                .Where(method => method.IsGenericMethodDefinition)
                .Where(method => method.GetGenericArguments().Length == 1)
                .Where(method => method.GetParameters().Length == 0)
                .FirstOrDefault();

            if (allMethod == null)
            {
                _warning("ProtosDb.All<T>() was not found.");
                return new List<object>();
            }

            try
            {
                var result = allMethod
                    .MakeGenericMethod(prototypeType)
                    .Invoke(protosDb, null) as IEnumerable;

                return result == null
                    ? new List<object>()
                    : result.Cast<object>().Where(item => item != null).ToList();
            }
            catch (Exception exception)
            {
                _warning("Could not enumerate " + fullTypeName + ": " + exception.Message);
                return new List<object>();
            }
        }

        private static Type ResolveType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore assemblies that cannot be inspected.
                }
            }

            return null;
        }

        private static bool ContainsId<T>(IEnumerable<T> items, string id)
        {
            return items.Any(item => string.Equals(
                GetContractId(item), id, StringComparison.Ordinal));
        }

        private static string GetContractId<T>(T item)
        {
            var property = typeof(T).GetProperty("Id");
            return property == null ? null : property.GetValue(item, null) as string;
        }

        private static string GetGameVersion()
        {
            var version = typeof(BaseMod).Assembly.GetName().Version;
            return version == null ? "unknown" : version.ToString();
        }

        private static string GetDefaultModRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Captain of Industry",
                "Mods",
                "coi-data-exporter");
        }

        private static void WriteJsonAtomically(
            object document,
            string outputPath)
        {
            var temporaryPath = outputPath + ".tmp";
            var json = JsonConvert.SerializeObject(document, Formatting.Indented);

            File.WriteAllText(
                temporaryPath,
                json,
                new UTF8Encoding(false));

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            File.Move(temporaryPath, outputPath);
        }

        private static void WriteConvenienceFiles(
            ExportDocument document,
            string outputDirectory)
        {
            WriteJsonAtomically(
                document.Products,
                Path.Combine(outputDirectory, "products.json"));
            WriteJsonAtomically(
                document.Recipes,
                Path.Combine(outputDirectory, "recipes.json"));
            WriteJsonAtomically(
                document.Buildings,
                Path.Combine(outputDirectory, "buildings.json"));
        }

        private static void WriteStatusFile(
            ExportDocument document,
            string outputDirectory,
            string outputPath)
        {
            var statusPath = Path.Combine(outputDirectory, "export-status.json");
            var status = new
            {
                success = true,
                schema_version = document.SchemaVersion,
                game_version = document.GameVersion,
                exported_at_utc = document.ExportedAtUtc,
                output_file = outputPath,
                products = document.Products.Count,
                recipes = document.Recipes.Count,
                buildings = document.Buildings.Count
            };

            File.WriteAllText(
                statusPath,
                JsonConvert.SerializeObject(status, Formatting.Indented),
                new UTF8Encoding(false));
        }

        private sealed class CategoryData
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
