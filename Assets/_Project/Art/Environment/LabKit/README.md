# Lab Kit (underground laboratory, level B-3)

Modular sci-fi lab pieces for the Demo-Area, in the same toon/flat look as the rest of the game.

## Where things are
- Prefabs, ready to drag into any scene: `Assets/_Project/Prefabs/Environment/LabKit/<Category>/`
  (Structure, Doors, Tech, Lights, Props ...). Each prefab has its colliders, lights and
  (for tall walls) a `FadeOccluder` already set up.
- The finished demo level: `Prefabs/Environment/Levels/Level_LabDemo.prefab`, grouped by area
  (Hub, Corridor, InnerRing, OuterRing, Sleeping, Technical, Bathroom, Kitchen, Storage, Lab, Elevator).
- The scene: `Scenes/Lab_Demo.unity` (player, enemies, HUD, iso camera, `Lab_Atmosphere`).
- Materials: `Art/Environment/Materials/Lab/` (Conquest/Toon Flat).
- Source: `Tools/Blender/build_lab_kit.py` (pieces in `lab/pieces_*.py`, layout in `lab/layout.py`).

## Menus
- **Conquest > Build Lab Demo** rebuilds prefabs, the level prefab and the scene.
- **Conquest > Rebuild Lab Kit Prefabs** rebuilds only the prefabs and the level prefab.
- **Conquest > Capture Lab Shots** writes reference renders to `Captures/Lab/`.

## Gates
Every `Gate_Blast`, `Gate_Shutter` and `Door_Sliding` has a `Gate` component.
Blast gates start closed and stay closed until your code opens them:

```csharp
using Conquest.Environment;

Gate.Open("Gate_Lab");          // slide open
Gate.Close("Gate_Lab");
Gate gate = Gate.Find("Gate_Elevator");
gate.Opened.AddListener(() => Debug.Log("elevator unlocked"));
gate.SetLocked(true);           // a locked door ignores its walk-in sensor
gate.SetOpenInstant(true);      // no animation (e.g. when loading a save)
```

Gate ids in the demo level:
| Id | Where |
| --- | --- |
| `Gate_InnerRing` | Inner ring to outer ring shortcut (north-east) |
| `Gate_Technical` | Outer ring into the technical maintenance room |
| `Gate_Lab` | End of the main corridor into the laboratory / boss arena |
| `Gate_Elevator` | Laboratory to the elevator up (exit after the boss) |

`Door_Sliding` pieces have `autoOpen` on: they open when a character walks into their sensor.
A gate you place yourself takes its id from the `gateId` field in the inspector.

## Sections (where am I, where do I go)
`Level_LabDemo` has a `LevelSections` component that splits it into Hub, Corridor, InnerRing,
OuterRing, Technical, Sleeping, Bathroom, Kitchen, Storage, Lab and Elevator.
- The section the player stands in is drawn normally; the rest of the level stays visible but
  dimmed (`dimTint`). Lamps, tubes and gate lights keep most of their glow (`glowKept`), so the
  way on stays readable.
- Walls of the current section that face the camera are cut down to a low base (`cutHeight`,
  0.6 m), like a cut-away drawing. They still cast their full shadow and still collide.
  Lamps, pipes and lockers hanging on a cut wall are cut with it. Gates and sliding doors are never cut; the sealed airlocks count as walls.
- Anything else between the camera and the player (a neighbouring section's wall, the reactor,
  tubes, lockers, racks) is cut too while it blocks the view (`cutLineOfSight`).
- Hold **Z / C** to turn the isometric camera around the player (it eases to the new angle).
  The cut-away follows the camera: walls that face it or stand sideways to it are cut (`cutFacing`,
  -0.35 by default), and so is every wall on the camera side of the player, also of the rooms next
  door, up to `cutNearSide` metres (18) away. Only the far walls stay standing.
- Enemies standing in another section are hidden until you walk in (`hideCharacters`).
- Untick `sectionsEnabled` to see the plain level while testing.

```csharp
var sections = FindFirstObjectByType<LevelSections>();
sections.SectionChanged += name => Debug.Log($"entered {name}");   // e.g. music or a room title
```

For your own level: add `LevelSections` to the level root, list the section names, make one
child per section with the same name and put that section's pieces under it. Add `SectionZone`
boxes (one or more per section) to mark where the player counts as being in it, and a
`SectionMember` on any wall or door that should belong to two sections. Walls are the pieces
with a `FadeOccluder` (all kit walls, pillars and rock).

## Building your own level
Pieces snap on a 4 m grid (walls are 4 m or 2 m long, 5 m high, 0.5 m thick; floors are 4 x 4 m).
Pivots sit on the floor at the centre of each piece, with the front facing -Z.
`Rock_Mass_4m` fills the space behind outer walls and fades out when it blocks the camera.
