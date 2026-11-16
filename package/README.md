# Skysail

**[English](#english) · [Português](#português)**

---

## English

Glide with the cape on your back. Jump off a cliff, press **Jump** in the air, and your cape carries you: your body leans into the wind, the cape streams behind you and white wind streaks rush past your sides. Better capes fly faster and tire you less.

### How to glide

- **Open:** press **Jump** while falling, at least 3 m above the ground, with some stamina left. Any cape works.
- **Steer:** the glide turns towards where you look.
- **W** dives (faster and steeper), **S** brakes (slower, a gentler fall).
- **Close:** press **Jump** again. The sail also closes by itself in water, when your stamina runs out, and 1 m above the ground, so you land with the game's own landing.
- While gliding, the walking keys do nothing, so the pose doesn't get bent.
- **Soft fall:** if you close the sail in the air, your next fall does half its damage, until you touch the ground.

### Capes

Every cape comes down at the same 2.4 m/s. A better cape flies further forward and costs less stamina.

| Tier | Capes | Forward | Stamina per second | Body lean |
|---|---|---|---|---|
| 1 | Deer Hide | 8 m/s | 3 | 35° |
| 2 | Troll Hide | 10 m/s | 2.4 | 40° |
| 3 | Wolf, Odin's cape, capes from other mods | 12 m/s | 1.9 | 45° |
| 4 | Lox, Linen | 14 m/s | 1.4 | 50° |
| 5 | Feather | 16 m/s | 1.0 | 55° |
| 6 | Asksvin, Ash, Deep North | 18 m/s | 0.7 | 60° |

Higher tiers also bring more wind streaks.

### The look

- Your character holds the game's own fall pose, leaning forward.
- The cape is the game's own cloth: while you glide, the air pushes it back and makes it flutter. When you land, it goes back to normal.
- Other players with the mod see you gliding the same way.

### Config

Press **F1** in game (Configuration Manager) or edit `BepInEx/config/felih.Skysail.cfg`.

- **1 - General:** height needed to open (3 m), stamina needed to open (5), how high above the ground the sail closes (1 m), how much damage the fall after closing does (0.5 = half).
- **2 - Look:** wind streaks on or off.
- **3 - Cape:** how the cape behaves while gliding. Changes show right away, even mid-flight.
  - **CapeStream:** how far the air pushes the cape back (0 = it hangs down, 1 = straight behind you).
  - **CapeGravity:** how heavy it is.
  - **CapeFlutter**, **CapeTurbulence**, **CapeFrequency:** how much, how unevenly and how fast it flutters.
  - **CapeMovingWind:** how much your own speed blows on it.
- **9 - Debug:** a detailed log and tools for the editors, all off.

### Install

Needs [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). With a mod manager (r2modman, Thunderstore), install it as usual. By hand: copy the `Skysail` folder from `plugins` into `Valheim/BepInEx/plugins/`.

**Client-side.** The server needs nothing. Players without the mod see you falling and landing normally.

---

## Português

Plane com a capa nas costas. Pule de um penhasco, aperte **Pular** no ar e a capa te leva: o corpo se inclina no vento, a capa voa atrás de você e traços brancos de vento passam pelos lados. Capas melhores voam mais rápido e cansam menos.

### Como planar

- **Abrir:** aperte **Pular** enquanto cai, a pelo menos 3 m do chão, com um pouco de stamina. Qualquer capa serve.
- **Virar:** o voo vira para onde você olha.
- **W** mergulha (mais rápido e mais inclinado), **S** freia (mais devagar, caindo mais suave).
- **Fechar:** aperte **Pular** de novo. A vela também fecha sozinha na água, quando a stamina acaba e a 1 m do chão, para você pousar com a aterrissagem do próprio jogo.
- Enquanto plana, as teclas de andar não fazem nada, para não entortar a pose.
- **Queda suave:** se você fechar a vela no ar, a próxima queda dá metade do dano, até você tocar o chão.

### Capas

Toda capa desce a 2,4 m/s. Uma capa melhor vai mais longe para a frente e gasta menos stamina.

| Tier | Capas | Para frente | Stamina por segundo | Inclinação do corpo |
|---|---|---|---|---|
| 1 | Couro de Cervo | 8 m/s | 3 | 35° |
| 2 | Couro de Troll | 10 m/s | 2,4 | 40° |
| 3 | Lobo, capa de Odin, capas de outros mods | 12 m/s | 1,9 | 45° |
| 4 | Lox, Linho | 14 m/s | 1,4 | 50° |
| 5 | Penas | 16 m/s | 1,0 | 55° |
| 6 | Asksvin, Cinzas, Extremo Norte | 18 m/s | 0,7 | 60° |

Tiers mais altos também trazem mais traços de vento.

### O visual

- O personagem segura a pose de queda do próprio jogo, inclinado para a frente.
- A capa é o tecido do próprio jogo: enquanto você plana, o ar empurra ela para trás e faz ela tremular. Quando você pousa, ela volta ao normal.
- Outros jogadores com o mod veem você planando do mesmo jeito.

### Configuração

Aperte **F1** no jogo (Configuration Manager) ou edite `BepInEx/config/felih.Skysail.cfg`.

- **1 - General:** altura para abrir (3 m), stamina para abrir (5), a que altura do chão a vela fecha (1 m), quanto dano dá a queda depois de fechar (0,5 = metade).
- **2 - Look:** traços de vento ligados ou não.
- **3 - Cape:** como a capa se comporta planando. Muda na hora, até no meio do voo.
  - **CapeStream:** quanto o ar empurra a capa para trás (0 = pendurada, 1 = reta atrás de você).
  - **CapeGravity:** o peso dela.
  - **CapeFlutter**, **CapeTurbulence**, **CapeFrequency:** quanto, quão irregular e quão rápido ela tremula.
  - **CapeMovingWind:** quanto a sua própria velocidade sopra nela.
- **9 - Debug:** log detalhado e ferramentas dos editores, tudo desligado.

### Instalar

Precisa do [BepInEx para Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). Com um gerenciador de mods (r2modman, Thunderstore), instale normalmente. À mão: copie a pasta `Skysail` de `plugins` para `Valheim/BepInEx/plugins/`.

**Client-side.** O servidor não precisa de nada. Quem não tem o mod vê você caindo e pousando normalmente.
