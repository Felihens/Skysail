# Skysail

A client-side BepInEx mod for Valheim: glide with any cape. Press Jump in the air and your cape carries you; better capes fly faster and tire you less, the cape's cloth streams in the wind and wind streaks pass by.

Mod para Valheim (BepInEx, client-side): plane com qualquer capa. Aperte Pular no ar e a capa te leva; capas melhores voam mais rápido e cansam menos, o tecido da capa voa no vento e traços de vento passam pelos lados.

For players, see [package/README.md](package/README.md). That file is also the Thunderstore page.
Para jogadores, veja [package/README.md](package/README.md).

Download: [GitHub Releases](https://github.com/Felihens/Skysail/releases)

Sister mods / mods irmãos: [Beastkeeper](https://github.com/Felihens/Beastkeeper), [Warband](https://github.com/Felihens/Warband), [Sea Legs](https://github.com/Felihens/SeaLegs), [Gather Tweaks](https://github.com/Felihens/GatherTweaks), [Auto Walk Around](https://github.com/Felihens/AutoWalkAround), [Recipe Book](https://github.com/Felihens/RecipeBook).

## Building

`sh build.sh` compiles `src/*.cs` against the game's assemblies (including MagicaClothV2 and Unity.Mathematics) and installs the dll into `BepInEx/plugins/Skysail/` (`sh build.sh nocopy` only compiles). Then `powershell -File pack.ps1` builds `dist/Skysail-<version>.zip` from `package/`.

## Glide editor

`editor/skysail_glide.template.html` plays back a glide recorded in the game (turn on `RecordGlide` in "9 - Debug"; it writes `BepInEx/config/Skysail_editor/glide_rec.json`) next to the exported character (`ExportCharacterAndCapes`). `python editor/make_glide.py` embeds both into `editor/skysail_glide.html`. The body pose can be edited there; the turns go into `GlideView.PoseTurns`.

## License

MIT
