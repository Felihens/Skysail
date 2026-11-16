# Builds skysail_glide.html: embeds the exported character and the glide recorded in the game.
import os
here = os.path.dirname(os.path.abspath(__file__))
cfg = r"D:/SteamLibrary/steamapps/common/Valheim/BepInEx/config/Skysail_editor/"
src = open(os.path.join(here, "skysail_glide.template.html"), encoding="utf-8").read()
def read(name):
    p = cfg + name
    return open(p, encoding="utf-8").read().replace("</", "<\\/") if os.path.exists(p) else "null"
out = src.replace("__PLAYER__", read("player.json")).replace("__REC__", read("glide_rec.json"))
open(os.path.join(here, "skysail_glide.html"), "w", encoding="utf-8").write(out)
print("built", len(out))
