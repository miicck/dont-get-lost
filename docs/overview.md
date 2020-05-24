
# Overview
This document provides an overview of how the different parts of the game fit together.

## World generator
The world generator has two levels: *chunks* and *biomes*. Biomes represent the geography as a`biome.point` grid, which is generated all in one go in`biome.generate_grid()`. To avoid causing the game to hang on the frame where a biome is generated, `biome.generate_grid` needs to be fast, so does not actually create any in-game objects; this is the job of the chunks. Chunks subdivide biomes into smaller sections which take the `biome.point` information and turn it into actual in-game objects, over the course of multiple frames. Biomes are generated automatically when a chunk needs the corresponding `biome.point` information (see **biome blending** below). Chunks within a biome start generating when they are within `game.render_range` of the player.

**Relevant classes:** `biome`, `chunk`, `biome.point`. **Relevant functions**:  `biome.generate`, `biome.generate_grid`, `biome.in_range`, `biome.Update`.

### Biome blending
When a chunk needs `biome.point` information near the edge of a biome, the adjacent biome is also generated and `biome.point` information from both biomes is blended together to create a seamless transition. This is the **only** mechanism by which the map expands when a player gets closer to the edge; world generation therefore roughly works like this:
1. The player walks near to the edge of a biome, triggering a chunk to generate.
2. That chunk needs adjacent biome information for blending, triggering generation of the adjacent biome.
3. The player can now walk into the adjacent biome, and chunks within that biome will start generating.
4. The player walks near to the edge of that biome, triggering another map extension, and so on...

Because only chunks near the edge of the biome need blending information, this process does not just continue recursively to infinity; only a finite region of the world is generated. The first biome is generated when the player loads in, and the world is extended from there.
**Relevant functions:** `world.start_generation`, `biome.blended_point`, `biome.point.average`.

## Networking

### Rewrite
I'm not 100% happy with the networking engine. If it were to be rewritten some nice features would be
1. The same representation of a network object is used both on the client and the server. This would allow the serialization/deserialization to be defined within the same object/would be generally simpler.
2. Redundant messages could be detected/not sent. For example, there is no point sending variable updates followed by a delete message.
3. Messages could be combined based on which network id they are intended for. This would reduce the overhead needed for metadata, and potentially also allow guarantees to be made about messages for the same network id arriving together. For example, it would be useful to guarantee that a create message followed by a gain authority message both arrive on the same frame, so we know about authority when creating the object.
