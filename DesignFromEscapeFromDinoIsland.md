The strongest design takeaway from the playsheets is: **this should be a survival-escape mystery roguelike, not primarily a dinosaur combat game.** The document constantly pushes toward weather, unreliable maps, broken infrastructure, role-based problem solving, countdowns, hard choices, and dinosaurs as intelligent animals with species-specific behavior rather than simple monsters. 

## 1. The island as a revealed, living map

Page 1 uses a blank island map where locations are added only when discovered or known: airstrip, aviary, beach, docks, fences, forest, hatchery, radio tower, river, swamp, tunnels, volcano, temporal anomaly, and so on.

For your roguelike, make the overworld **partially unknown**. The player begins with a rough tourist/company map, but exact locations, paths, fences, nests, gates, utility sheds, and danger zones are discovered through exploration, vantage points, radio chatter, documents, or NPCs.

Useful mechanics:

| Source idea                               | Roguelike implementation                                                        |
| ----------------------------------------- | ------------------------------------------------------------------------------- |
| “Add to the map” when locations are known | Map tiles start vague, then become named landmarks                              |
| Natural + artificial locations            | Mix jungle, cliffs, lakes, roads, labs, fences, tunnels                         |
| Countdowns on map sheet                   | Global timers for storm, power failure, evacuation, volcano, predator migration |
| Weather table                             | Weather changes stealth, noise, visibility, scent, tracking, electronics        |

## 2. Start each run with a scenario generator

Page 2 has a great “Here’s the Situation” questionnaire: why the party came, where they start, what the escape route is, why they cannot leave, and what mystery needs solving.

Turn this into your roguelike’s **run seed generator**.

Example run setup:

```text
Mission: rescue/retrieval
Start: abandoned dormitories
Known escape: offshore yacht
Obstacle 1: monorail is offline
Obstacle 2: raptor territory blocks the maintenance route
Mystery: why are radio signals being overpowered?
First dinosaur encounter: sick triceratops acting strangely
Island secret: the old lab was not making dinosaurs — it was modifying behavior
```

This gives each run a strong identity without requiring a huge authored campaign.

## 3. Two-layer victory: escape + mystery

The document separates practical obstacles from mysteries. That is very useful for a roguelike.

A simple roguelike victory could be:

1. Find a way off the island.
2. Overcome the physical obstacle.
3. Solve enough of the mystery to unlock the safest/best ending.

Escaping without solving the mystery could still count as survival, but produce a worse ending: the dinosaurs spread, the villain escapes, the wrong data gets recovered, the park reopens, or the player is blamed.

## 4. Peril moves become tactical action verbs

Page 3’s basic moves translate very cleanly into top-down gameplay actions.

| Tabletop move        | Video game mechanic                                                      |
| -------------------- | ------------------------------------------------------------------------ |
| Run                  | Sprint/retreat to a nearby revealed tile, with injury/noise risk         |
| Hide                 | Break line of sight, hide in tall grass, vehicles, lockers, ducts        |
| Look Over There      | Throw object, trigger speaker, flare, goat bait, car alarm               |
| Take My Hand         | Pull NPC/player companion across gap, door, fence, mud, river            |
| Fight                | Last-resort stun/delay action, not reliable dinosaur killing             |
| Just Do It           | Timed interaction under pressure: unlock door, reboot panel, climb fence |
| Hold On to Your Butt | Push through injury, exhaustion, poison, fear, storm, panic              |
| Scavenge             | Search rooms/corpses/vehicles, but generate noise or scent               |
| Lay of the Land      | Climb tower/tree/cliff to reveal landmarks                               |
| Instruct             | Guide an NPC through a task remotely by radio                            |

This suggests a roguelike where your main tactical verbs are **escape, hide, distract, repair, scavenge, guide, and risk injury**, rather than simply attack.

## 5. “The Best-Laid Plans” as an anti-turtling mechanic

The sheet says that when players take too long planning, the DM makes a move. That is perfect for a real-time-with-pause or turn-based roguelike.

Implementation idea: every turn spent in planning menus, inventory management, map mode, or repeated waiting advances an **Island Pressure Clock**.

Pressure events:

* Distant roar gets closer.
* Weather worsens.
* Herd stampedes through the area.
* Power grid loses another sector.
* A wounded NPC starts bleeding out.
* Raptors investigate noise.
* The escape vehicle leaves in X turns.
* A door lock cycles shut.
* A predator loses interest in bait and resumes tracking you.

This keeps the player from solving everything safely from menus.

## 6. Dinosaurs need “gimmicks,” not just stats

Page 4 says every dinosaur species should have a gimmick and emphasizes that dinosaurs are animals, not monsters.

For a Jurassic-style roguelike, each dinosaur species should have a distinct behavioral puzzle:

| Dinosaur type             | Possible gimmick                                                                          |
| ------------------------- | ----------------------------------------------------------------------------------------- |
| Tyrannosaur               | Poor fine tracking in heavy rain, drawn to vibration/noise, terrifying but not omniscient |
| Raptors / Deinonychus     | Flank, test fences, retreat and re-ambush, learn from failed traps                        |
| Triceratops               | Territorial charge, blocks roads, can be calmed or redirected                             |
| Dilophosaur-like predator | Keeps distance, blinds/spits, prefers ambush                                              |
| Compies                   | Weak alone, dangerous as corpse/bleeding/noise swarms                                     |
| Pterosaurs                | Attack exposed open ground, avoid enclosed jungle/labs                                    |
| Sauropods                 | Environmental hazard: panic causes trampling, not “enemy” behavior                        |
| Ankylosaur                | Slow defensive obstacle, dangerous if cornered                                            |
| Carnotaurus-style stalker | Uses camouflage/low visibility zones                                                      |
| Aquatic predator          | Turns rivers/lakes into route-planning hazards                                            |

The important thing is that dinosaurs should create **situational problems**, not just damage races.

## 7. Environment should be as dangerous as dinosaurs

The document’s DM principles explicitly elevate the environment: storms, cliffs, rivers, locked complexes, monorails, tunnels, ruins, swamps, fences, and power systems all matter.

Good roguelike hazards:

* Electrified fences: safe when power is out, lethal when restored.
* Mud/swamp: slows player, preserves tracks, increases scent.
* Rain: reduces visibility and sound range, washes away tracks.
* Lightning: disables electronics, starts fires, spooks herds.
* Tall grass: hides both player and predators.
* Jungle: blocks long sightlines and ranged weapons.
* Monorail: fast travel route if repaired, deathtrap if stalled.
* Maintenance tunnels: safer from large dinosaurs, worse for small pack hunters.
* Labs: loot-rich but alarm-heavy and maze-like.
* Aviary: vertical threat zone with open-roof exposure.

## 8. Character archetypes as build classes

Pages 5–11 include Doctor, Engineer, Hunter, Kid, Paleontologist, Soldier, and Survivor playsheets. These are excellent roguelike classes.

| Class          | Roguelike role                                                       |
| -------------- | -------------------------------------------------------------------- |
| Doctor         | Treats wounds, stabilizes NPCs, identifies toxins/disease            |
| Engineer       | Repairs fences, generators, radios, vehicles, doors                  |
| Hunter         | Tracks dinosaur movement, sets traps, reads spoor                    |
| Kid            | Small size, vents/crawling, “I know this!” surprise skill moments    |
| Paleontologist | Identifies species behavior, weaknesses, drives, pack size           |
| Soldier        | Best at weapons, suppressive fire, escort missions, but ammo-limited |
| Survivor       | Knows island shortcuts, hidden shelters, edible plants, caches       |

This is especially good if you want multiple playable characters or recruitable NPC companions.

## 9. Injuries instead of simple HP

The playsheets use injuries, out-of-commission states, and casualty choices rather than a normal hit point grind.

That fits dinosaurs perfectly. A raptor bite should not remove “12 HP”; it should create a problem.

Possible injury system:

* Limping: slower movement, worse climbing/running.
* Bleeding: leaves scent trail, worsens over time.
* Concussion: unreliable map/vision/audio cues.
* Broken arm: cannot use rifles, tools, or climb well.
* Panic: involuntary noise or failed stealth checks.
* Infection/venom: ticking clock unless treated.
* Out of commission: companion must carry, drag, or stabilize you.

This makes every dinosaur encounter scary even if the player survives.

## 10. Story prompts as memory/trait unlocks

The document repeatedly uses “Stories You Tell” as a mechanic: characters reveal memories under pressure.

For a roguelike, convert that into **flashback traits**. When the player rests, treats wounds, instructs someone, or faces a crisis, they choose/reveal a backstory card.

Example:

```text
Flashback: “A time you were forced to cut corners.”
Unlock: Improvised Repair
Effect: Once per run, repair a system without the correct part, but it may fail later.
```

This gives narrative progression without long cutscenes.

## 11. Rumors and unreliable knowledge

The setup gives each hero a rumor. That is a great procedural content hook.

Before each run, give the player 2–3 rumors:

* “The old east dock still has a working fuel pump.”
* “The raptors avoid the geothermal vents.”
* “The radio tower is broadcasting, but not human voices.”
* “One of the fences was never connected to the main grid.”
* “There is a hidden smuggler runway north of the swamp.”

Some should be true, some partly true, some outdated. This creates exploration goals and tension.

## 12. Finale system: unresolved threats return

Page 4 has a “For Later” space for unresolved enemies, problems, locations, and NPCs that can return in the finale.

For your game, track unresolved threats during the run:

* Predator pack you escaped but did not neutralize.
* NPC you abandoned.
* Alarm you triggered.
* Fence you cut.
* Fire you started.
* Rival human faction you angered.
* Dinosaur nest you disturbed.
* Vehicle you damaged but left behind.

Then use 2–4 of them in the endgame escape sequence. This makes the finale feel authored even when procedural.

## Strongest design direction

I would build the game around this loop:

```text
Explore island →
Discover landmarks/clues →
Scavenge tools →
Avoid or redirect dinosaurs →
Repair or unlock infrastructure →
Solve mystery enough to identify escape route →
Survive escalating extinction/countdown event →
Final escape using everything you affected earlier
```

The most valuable ideas from the document are **procedural scenario setup, map revelation, role-based survival actions, dinosaur behavioral gimmicks, injuries over HP, weather/countdown pressure, and finales built from unresolved consequences**.
