using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CoiDataExporter.Contracts;
using Newtonsoft.Json;

namespace CoiDataExporter.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            var command = args[0].ToLowerInvariant();
            if (command == "help" || command == "--help" || command == "-h")
            {
                PrintUsage();
                return 0;
            }

            try
            {
                switch (command)
                {
                    case "validate":
                        return Validate(RequireArgument(args, 1, "input file"));
                    case "summary":
                        return Summary(RequireArgument(args, 1, "input file"));
                    case "csv":
                        return ExportCsv(
                            RequireArgument(args, 1, "input file"),
                            args.Length > 2 ? args[2] : Path.Combine(
                                Path.GetDirectoryName(Path.GetFullPath(args[1])) ?? ".",
                                "csv"));
                    default:
                        // A path without a command is treated as validate for convenient scripting.
                        return Validate(args[0]);
                }
            }
            catch (CliException exception)
            {
                Console.Error.WriteLine("Error: " + exception.Message);
                return exception.ExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }

        private static int Validate(string inputPath)
        {
            var document = Load(inputPath);
            var errors = ExportValidator.Validate(document);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine("INVALID: " + error);
                }

                return 1;
            }

            Console.WriteLine("Valid export.");
            Console.WriteLine("Game version: " + document.GameVersion);
            Console.WriteLine("Products:     " + document.Products.Count);
            Console.WriteLine("Recipes:      " + document.Recipes.Count);
            Console.WriteLine("Buildings:    " + document.Buildings.Count);
            return 0;
        }

        private static int Summary(string inputPath)
        {
            var document = Load(inputPath);
            Console.WriteLine("COI Data Export");
            Console.WriteLine("===============");
            Console.WriteLine("Schema:        " + document.SchemaVersion);
            Console.WriteLine("Game version:  " + document.GameVersion);
            Console.WriteLine("Exported UTC:  " + document.ExportedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            Console.WriteLine("Products:      " + document.Products.Count);
            Console.WriteLine("Recipes:       " + document.Recipes.Count);
            Console.WriteLine("Buildings:     " + document.Buildings.Count);
            return 0;
        }

        private static int ExportCsv(string inputPath, string outputDirectory)
        {
            var document = Load(inputPath);
            var errors = ExportValidator.Validate(document);
            if (errors.Count > 0)
            {
                throw new CliException(
                    "Input is invalid. Run 'validate' for details.",
                    1);
            }

            Directory.CreateDirectory(outputDirectory);
            CsvExporter.Write(document, outputDirectory);
            Console.WriteLine("CSV files written to: " + Path.GetFullPath(outputDirectory));
            return 0;
        }

        private static ExportDocument Load(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new CliException("Input path is empty.", 2);
            }

            var fullPath = Path.GetFullPath(inputPath);
            if (!File.Exists(fullPath))
            {
                throw new CliException("Input file not found: " + fullPath, 2);
            }

            try
            {
                var json = File.ReadAllText(fullPath, Encoding.UTF8);
                var document = JsonConvert.DeserializeObject<ExportDocument>(json);
                if (document == null)
                {
                    throw new CliException("Input JSON is empty.", 2);
                }

                return document;
            }
            catch (JsonException exception)
            {
                throw new CliException("Input is not valid JSON: " + exception.Message, 2);
            }
        }

        private static string RequireArgument(
            IReadOnlyList<string> args,
            int index,
            string name)
        {
            if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
            {
                throw new CliException("Missing " + name + ".", 2);
            }

            return args[index];
        }

        private static void PrintUsage()
        {
            Console.WriteLine("CoiDataExporter CLI");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  coi-data-exporter validate <coi-data.json>");
            Console.WriteLine("  coi-data-exporter summary  <coi-data.json>");
            Console.WriteLine("  coi-data-exporter csv      <coi-data.json> [output-directory]");
            Console.WriteLine();
            Console.WriteLine("The CLI is deliberately independent from the game DLLs.");
        }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    internal static class ExportValidator
    {
        public static List<string> Validate(ExportDocument document)
        {
            var errors = new List<string>();
            if (document == null)
            {
                errors.Add("Document is null.");
                return errors;
            }

            if (document.SchemaVersion != 1)
            {
                errors.Add("Unsupported schema_version: " + document.SchemaVersion);
            }

            ValidateUnique(errors, "product", document.Products.Select(item => item.Id));
            ValidateUnique(errors, "recipe", document.Recipes.Select(item => item.Id));
            ValidateUnique(errors, "building", document.Buildings.Select(item => item.Id));

            var productIds = new HashSet<string>(
                document.Products
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => item.Id),
                StringComparer.Ordinal);
            var recipeIds = new HashSet<string>(
                document.Recipes
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => item.Id),
                StringComparer.Ordinal);

            foreach (var recipe in document.Recipes.Where(item => item != null))
            {
                if (recipe.DurationSeconds <= 0)
                {
                    errors.Add("Recipe has a non-positive duration: " + recipe.Id);
                }

                ValidateQuantities(errors, "recipe " + recipe.Id + " input", recipe.Inputs, productIds);
                ValidateQuantities(errors, "recipe " + recipe.Id + " output", recipe.Outputs, productIds);
            }

            foreach (var building in document.Buildings.Where(item => item != null))
            {
                ValidateQuantities(errors, "building " + building.Id + " cost", building.BuildCosts, productIds);

                foreach (var recipeId in building.RecipeIds ?? new List<string>())
                {
                    if (!recipeIds.Contains(recipeId))
                    {
                        errors.Add("Building " + building.Id + " references missing recipe: " + recipeId);
                    }
                }
            }

            return errors;
        }

        private static void ValidateQuantities(
            ICollection<string> errors,
            string context,
            IEnumerable<ProductQuantityDto> quantities,
            ISet<string> productIds)
        {
            foreach (var quantity in quantities ?? Enumerable.Empty<ProductQuantityDto>())
            {
                if (quantity == null || string.IsNullOrWhiteSpace(quantity.ProductId))
                {
                    errors.Add(context + " contains a quantity without a product ID.");
                    continue;
                }

                if (!productIds.Contains(quantity.ProductId))
                {
                    errors.Add(context + " references missing product: " + quantity.ProductId);
                }

                if (quantity.Amount <= 0)
                {
                    errors.Add(context + " has a non-positive amount for: " + quantity.ProductId);
                }
            }
        }

        private static void ValidateUnique(
            ICollection<string> errors,
            string type,
            IEnumerable<string> ids)
        {
            var duplicateIds = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateId in duplicateIds)
            {
                errors.Add("Duplicate " + type + " ID: " + duplicateId);
            }
        }
    }

    internal static class CsvExporter
    {
        public static void Write(ExportDocument document, string directory)
        {
            WriteFile(
                Path.Combine(directory, "products.csv"),
                new[] { "id", "name", "state", "is_storable", "is_trash", "radioactivity" },
                document.Products.Select(product => new[]
                {
                    product.Id,
                    product.Name,
                    product.State,
                    product.IsStorable.ToString(),
                    product.IsTrash.ToString(),
                    product.Radioactivity.ToString(CultureInfo.InvariantCulture)
                }));

            WriteFile(
                Path.Combine(directory, "recipes.csv"),
                new[] { "id", "name", "duration_seconds" },
                document.Recipes.Select(recipe => new[]
                {
                    recipe.Id,
                    recipe.Name,
                    recipe.DurationSeconds.ToString(CultureInfo.InvariantCulture)
                }));

            WriteFile(
                Path.Combine(directory, "recipe_inputs.csv"),
                new[] { "recipe_id", "product_id", "product_name", "amount" },
                document.Recipes.SelectMany(recipe => recipe.Inputs.Select(input => new[]
                {
                    recipe.Id,
                    input.ProductId,
                    input.ProductName,
                    input.Amount.ToString(CultureInfo.InvariantCulture)
                })));

            WriteFile(
                Path.Combine(directory, "recipe_outputs.csv"),
                new[] { "recipe_id", "product_id", "product_name", "amount" },
                document.Recipes.SelectMany(recipe => recipe.Outputs.Select(output => new[]
                {
                    recipe.Id,
                    output.ProductId,
                    output.ProductName,
                    output.Amount.ToString(CultureInfo.InvariantCulture)
                })));

            WriteFile(
                Path.Combine(directory, "buildings.csv"),
                new[] { "id", "name", "category_id", "workers", "electricity_consumed", "electricity_generated", "storage_capacity" },
                document.Buildings.Select(building => new[]
                {
                    building.Id,
                    building.Name,
                    building.CategoryId,
                    building.Workers.ToString(CultureInfo.InvariantCulture),
                    building.ElectricityConsumed.ToString(CultureInfo.InvariantCulture),
                    building.ElectricityGenerated.ToString(CultureInfo.InvariantCulture),
                    building.StorageCapacity.ToString(CultureInfo.InvariantCulture)
                }));
        }

        private static void WriteFile(
            string path,
            IEnumerable<string> header,
            IEnumerable<IEnumerable<string>> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(string.Join(",", header.Select(Escape)));
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(",", row.Select(Escape)));
                }
            }
        }

        private static string Escape(string value)
        {
            var text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
