# Builds skysail_capes.html: embeds the exported character and the pose saved in the wingsuit editor.
import os, json
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "skysail_capes.template.html"), encoding="utf-8").read()
pj = r"D:/SteamLibrary/steamapps/common/Valheim/BepInEx/config/Skysail_editor/player.json"
player = open(pj, encoding="utf-8").read().replace("</", "<\/") if os.path.exists(pj) else "null"
pose = json.load(open(os.path.join(here, "pose.json"), encoding="utf-8"))
fj = r"D:/SteamLibrary/steamapps/common/Valheim/BepInEx/config/Skysail_editor/fall.json"
fall = open(fj, encoding="utf-8").read() if os.path.exists(fj) else "null"
out = src.replace("__PLAYER__", player).replace("__POSE__", json.dumps(pose)).replace("__FALL__", fall)
open(os.path.join(here, "skysail_capes.html"), "w", encoding="utf-8").write(out)
print("built", len(out), "pose", pose)
