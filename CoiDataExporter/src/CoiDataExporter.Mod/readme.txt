COI Data Exporter
=================

This mod exports the runtime prototype database to:

  export/coi-data.json       (complete export)
  export/products.json       (products only)
  export/recipes.json        (recipes only)
  export/buildings.json      (buildings only)
  export/export-status.json
  export/export-error.txt    (only on failure)

The files are written below this mod's directory in:

  %APPDATA%\Captain of Industry\Mods\coi-data-exporter

Enable the mod, then create a new game. The export is generated while the game initializes.

The external CLI can validate and convert the JSON snapshot to CSV.
