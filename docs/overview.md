
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
The networking model is a client-server model with client authority. The server does not carry out a simulation of the game in any way, it simply serves to syncronise data between clients/maintains a copy of the data to save the game. There is no distinction between saved data and networked data; if it is networked - it is saved. Because of this a server must be running, even on local games, to save data. Playing the game in singleplayer therefore starts both a client and a server under the hood.

### Operation
The networking engine defines two base classes `networked` (inheriting from `MonoBehaviour`) and `networked_variable` (not inheriting from anything). `networked` objects are created by clients when needed, and will automatically be sent to the server and created on other clients. `networked_variable`s contained within a `networked` object will automatically be syncronised across the network.

**Relevant classes:** `networked`, `networked_variable`, `client`, `server`

#### Proximity tests
Players are represented by a special kind of `networked` object, a `networked_player` which defines a single additional field `render_range`. To determine which `networked` objects need to be loaded on a particular client, the server tests if it is within `render_range` of the player on that client. Objects within `render_range` are automatically loaded, and objects beyond `render_range` are automatically unloaded. `networked` objects may be made children of one another, in which case proximity tests will only be applied to the top-level object; children will be loaded/unloaded with their parents. This is useful when networked objects depend on one another. For example, a `character_spawner` needs to know about the `character`s that it has spawned, in order to detimine if more need to be spawned; the spawned `character`s are therefore spawned as children of the `character_spawner`.

**Relevant classes:** `networked_player`, `character_spawner`, `character`

#### Networked object creation
Clients are in full control of creating whatever `networked` objects that they want via the `client.create` method. Calls to `client.create` can't be rejected by the server, so it is safe for the clients to go ahead and start using created objects immediately. This allows the client-side response to `networked` creation to be instantaneous/smooth, even if there is a large ping to the server. A side effect of this is that there is **no way** to test for object existance on the server; clients are responsible for making sure that they do not spawn the same object multiple times. This does not present a problem for player-triggered events, such as a player building something, because the object built is guaranteed to be new to the game. However, the lack of existance checks means that non-unique events (i.e those that might happen multiple times in exactly the same way, such as the world being generated) must wait until all relevant objcets that exist on the server have been loaded before creating `networked` objects. For example, a `world_object` that spawns a `networked` object must first wait to see if the object to spawn gets loaded from the server (i.e it was already spawned on another client).

This can be seen in action by considering a few examples. First, lets imagine that, when the world is generated, client A wishes to spawn a lever that can be pulled by the players (which has it's state networked). If the world called `client.create` to create the lever without any checks then every time the world is re-generated (i.e if the player walks away and comes back), a new lever would be created, leading to multiple unwanted levers. Instead, the object wishing to create the lever must wait to see if a lever (matching the description of the one that it wishes to create) appears, loaded from the server.

The `networked_object_spawner` class is a type of `world_object` designed to allow spawning of `networked` objects in this way. The networked objects spawned by a `networked_object_spawner` will be of the `spawned_by_world_object` type, and contain the coordinates of the `networked_object_spawner` that spawned them. This is so that, when they are loaded on a remote client, they can identify the `networked_object_spawner` that they should associate themselves with and tell it that they exist (stopping the `networked_object_spawner` from spawning them again).

**Relevant classes:** `networked_object_spawner`, `spawned_by_world_object`.
**Relevant functions:** `client.create`

### Rewrite
I'm not 100% happy with the networking engine. If it were to be rewritten some nice features would be
1. The same representation of a network object is used both on the client and the server. This would allow the serialization/deserialization to be defined within the same object/would be generally simpler.
2. Redundant messages could be detected/not sent. For example, there is no point sending variable updates followed by a delete message.
3. Messages could be combined based on which network id they are intended for. This would reduce the overhead needed for metadata, and potentially also allow guarantees to be made about messages for the same network id arriving together. For example, it would be useful to guarantee that a create message followed by a gain authority message both arrive on the same frame, so we know about authority when creating the object.
4. <s>Some sort of mechanism allowing objects to expect replies from the server, and to avoid deleting the objects until they have reccived the reply. This would allow us to determine if a missing recipient for a message is a bug or not. (however, this might not be neccassary, as the server maintains an authorative serialization of the objects).</s> Implemented for object deletion.
