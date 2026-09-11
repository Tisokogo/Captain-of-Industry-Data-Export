using System;
using System.IO;
using Mafi;
using Mafi.Base;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace CoiDataExporter.Mod
{
    /// <summary>
    /// Runtime bridge. The data is intentionally exported from inside the game because
    /// ProtosDb is populated by the game at runtime and is not available to an external CLI.
    /// </summary>
    public sealed class CoiDataExporterMod : DataOnlyMod, IMod
    {
        private readonly ModManifest _manifest;

        public CoiDataExporterMod(ModManifest manifest) : base(manifest)
        {
            _manifest = manifest;
            Log.Info("CoiDataExporter: constructed.");
        }

        public override void RegisterPrototypes(ProtoRegistrator registrator)
        {
            // This is a data-only exporter. It does not register game content.
            Log.Info("CoiDataExporter: no prototypes to register.");
        }

        // The current mod API exposes the populated prototype database in this lifecycle step.
        // Keeping the explicit implementation prevents accidental duplicate entry points.
        void IMod.RegisterDependencies(
            DependencyResolverBuilder dependencyBuilder,
            ProtosDb protosDb,
            bool gameWasLoaded)
        {
            try
            {
                Log.Info(gameWasLoaded
                    ? "CoiDataExporter: save-game load detected; exporting runtime data."
                    : "CoiDataExporter: game initialization detected; exporting runtime data.");

                var outputRoot = _manifest == null ? null : _manifest.RootDirectoryPath;
                var pipeline = new ExportPipeline(outputRoot, LogInfo, LogWarning);
                pipeline.Run(protosDb);

                Log.Info("CoiDataExporter: export completed.");
            }
            catch (Exception exception)
            {
                Log.Warning("CoiDataExporter: export failed.");
                Log.Warning(exception.ToString());
                WriteErrorReport(_manifest == null ? null : _manifest.RootDirectoryPath, exception);
            }
        }

        private static void WriteErrorReport(string modRoot, Exception exception)
        {
            try
            {
                var root = string.IsNullOrWhiteSpace(modRoot)
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Captain of Industry",
                        "Mods",
                        "coi-data-exporter")
                    : modRoot;
                var exportDirectory = Path.Combine(root, "export");
                Directory.CreateDirectory(exportDirectory);
                File.WriteAllText(
                    Path.Combine(exportDirectory, "export-error.txt"),
                    DateTime.UtcNow.ToString("O") + Environment.NewLine + exception,
                    System.Text.Encoding.UTF8);
            }
            catch
            {
                // Never let diagnostics hide the original export error.
            }
        }

        private static void LogInfo(string message)
        {
            Log.Info(message);
        }

        private static void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public override void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
        {
            // The exporter has no persisted game configuration.
        }
    }
}
