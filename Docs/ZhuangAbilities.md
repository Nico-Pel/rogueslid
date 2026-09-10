# Zhuang: implementation and validation

The original design document and the detailed follow-up answers take precedence over the old placeholder descriptions. There are 13 abilities: the 12 existing assets plus the missing Black Turtle Step. Zhuang starts with Wild Fauchard, Side Step, and an empty special slot.

| Ability | Implemented behavior |
| --- | --- |
| Wild Fauchard | 5 melee damage; adjacent enemies, rocks, barrels and skulls. Separate swing and impact feedback. |
| Dragon Guandao | 5 damage adjacent, 7 at distance 2; distant survivors launch in place. Diagonal upgrade, +1 damage x3, zero-movement whirlwind, delayed execution at exactly 2 HP. Skip the follow-up if the whirlwind killed the target. |
| Tiger Fists | 5 damage and a one-cell push. Blocked pushes cause a separate hit after 0.35 seconds and launch. Stone Tiger breaks rocks and hits the enemy behind them; Tiger Claws pulls the weakest candidate. Self-targeting with White Tiger spends the base use to grant an unused temporary attack: +2 damage, two-cell push, 2 HP cost. Fierce Fist adds 2 collision damage per stack, max 2. |
| Phoenix Jian | Three-cell frontal sweep, 4 base damage. Phoenix Wing adds the side tiles. Tongue of Fire applies a non-stacking 2-damage burn at the end of each enemy's turn; burn kills grant extra uses next player turn. Blazing Strike adds damage per target, max 2 stacks. Phoenix Flight launches survivors and reduces mobility when multiple enemies are hit. |
| Jade Column | A blocking Rock at exactly radial distance 2, lasting until the next player turn. Jade Eye allows adjacent placement and increases maximum range to 3. Imperial Projection allows placement under an enemy, launches it, collapses after 0.25 seconds and deals 1 landing damage. Jade Burst deals 2 area damage and breaks neighboring obstacles, without hurting Zhuang. |
| Crane Punishment | A nearby launch opens 2.5 real seconds of Momentum, with slow motion, cyan button feedback and a screen frame. The cast previews its radius, deals 4 radial damage and spends all movement. Unlimited uses, but needs Momentum and a target. Winged Whirlwind: +1 damage x3; Broken Wings: mobility reduction with floor 1; Grace: heal per movement spent. |
| Lion's Roar | Reduces resistance of enemies beyond distance 3 until player turn end. Beastly Fear doubles reduction beyond 5, with yellow button feedback. Velvet Step lowers the minimum distance. Lion Startle marks targets: +1 added to ability hits, then launches survivors. Resounding Roar deals immediate damage, max 3 stacks. |
| Side Step | Starting mobility: one adjacent step costing one movement point. Prepared activation can be cancelled. Has its own step FX and sound. |
| Black Turtle Step | Separate unlockable mobility. One-cell move and +1 resistance until the enemy turn ends. Sacred Carapace adds +1 resistance per hit received; Spiked Carapace retaliates per impact, max 3 stacks; Double Step extends straight-line range to 2. Cooldown 2. |
| Monkey Strike | Leap to a free tile adjacent to any enemy. Agility allows diagonal adjacency; Prankster allows using Zhuang as the anchor. Silver Strike damages around the departure cell. Golden Strike only triggers when the landing strike can kill: 2 area damage and an extra use this turn. Cooldown 2. |
| Serpent Hook | Straight projectile stopped by the first obstacle or enemy; rush to the preceding cell. At point-blank range, feint in place. 1 damage plus Fangs stacks. Flying Serpent launches after the movement; Twin Serpents grants a second use per turn. |
| Celestial Rings | Starts empty; each launch grants a charge, retained between turns. 2-damage straight projectile, 0.2-second reuse delay. Gift heals per kill; Rebound visually returns a lethal projectile before restoring its charge. Divine Rings promotes the whole stock at 3 and stays promoted until empty, with +1 damage and splash against enemies/obstacles. Fetish starts combat with one ring. |
| Heron Wings | Glide to a free tile within radius 2. Great Wings adds range, max 2 stacks. Heron Glide grants temporary uses on launches. Divine Feather heals when landing beside at least two enemies; Feather Whirlwind damages adjacent enemies, max 3 stacks. |

## Shared behavior and setup

- `ZhuangAbilityUtils.Launch` is the shared launch event used by Crane, Rings and Heron. Dragon Cloud suppresses Momentum for its automatic execution.
- Per-ability state belongs to `CharacterAbilityRuntime`, so charges and temporary transformations do not leak across characters or combats.
- Delayed actions hold an action lock. Jade's airborne landing is asynchronous so the player can act during Momentum.
- Real movement uses a single tween, including leap arcs. Leaps ignore crossed ground hazards but trigger the landing tile's hazard. Board occupancy is updated through the existing movement API.
- All new ability assets point to concrete scripts. Upgrade descriptions, stacking limits and ownership match the original design.
- Zhuang's 11 skill-unlock rewards are available by default. The three future progression-locked skills have not been chosen, so the entire kit must not be excluded from level-end rewards. After choosing a skill category, rewards offer its equipped skill's upgrades as before.
- FX and sound references reuse existing game assets intentionally. Invalid prefab file IDs and the incorrectly typed Guandao airborne sound were repaired. Jade uses a breakable pillar placeholder and a green burst effect.
- The home menu now references the existing Zhuang character prefab.

## Animation setup

- All assigned ability clips come from `Zhuang_Animations`, not another character's rig. Fauchard, Guandao and Serpent use the spear strike; Tiger uses fists; Phoenix uses the sword; Crane uses the radial strike; Rings, Lion and Jade use incantation.
- Side Step has no activation attack. Side Step, Black Turtle, Monkey and Heron use the controller's Dash state during movement. Monkey's radial strikes only play when their Silver/Golden upgrades actually trigger; Heron's radial strike requires Feather Whirlwind and a target. White Tiger's transformation uses incantation rather than a punch.
- The character prefab's attack placeholder matches the actual Attack state's `Attack_Front_Spear` clip. Attack can transition into Dash when movement starts, including Serpent's pull.

## Active item regressions

- Void Glove enters free-cell targeting, validates range and empty destinations, and pulls the weakest neighboring enemy. Equal-health candidates prioritize proximity to the player, with random selection only for remaining ties on activation, never during previews.
- Switching between a character skill and an active item clears the previous targeting slot.
- Adding/consuming potions and compacting bonus slots preserve other runtime instances, cooldowns and turn/combat use counters.

## Verification

`Assets/Editor/ZhuangValidation.cs` contains the asset checks and isolated Play Mode scenarios. The asset-only menu is **Tools > Rogue Sliders > Validate Zhuang Asset References**.

The complete suite is intended for an isolated project copy, not an editor session with unsaved scenes:

```sh
Unity -batchmode -nographics -projectPath /path/to/isolated/project -executeMethod ZhuangValidation.Run -logFile /tmp/zhuang-unity.log
```

The batch entry point compiles/imports the actual assets, opens an empty scene, tests gameplay, writes `zhuang-validation.txt` to the system temporary directory and exits with 0 on success. It does not load or save player progression.

Final validation on Unity 2022.3.62f2 passed with 648 assertions and process exit code 0. It covers reference resolution, audio clips, upgrade ownership, all abilities, main combos, action locks, resource expiration, leap hazards and Momentum clock restoration. Regression checks also cover screen-click targeting for Void Glove, the bow, knife and five potions, inventory state preservation, 20 level-end reward draws, upgrade offers, and clips actually played through Zhuang's controller. The detailed results are in `ZhuangValidationReport.txt`. The tests run headlessly: final animation choreography, appearance on devices and audio mixing still require a visual/listening pass. Existing FX and meshes remain replaceable placeholders; ability animations now use Zhuang's own supplied clips.
