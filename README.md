# Don't get lost
A procedurally generated video game.
## Developing
To setup a unity project for development, follow these steps:
1. Using the latest version of unity, create a new unity project using the *High Definition RP* template.
2. Replace the Assets/ folder (in its entirety) with a clone of this repository. <br>
    Linux steps:
    ~~~~
    $ cd /path/to/project/folder
    $ git clone https://github.com/miicck/dont-get-lost
    $ rm -r Assets
    $ mv dont-get-lost Assets
    ~~~~
 
3. Load the scene Assets/scenes/project_setup, select the object called *setup* in the heirarchy and click *Run setup* in the inspector.
4. (optional, recommended) Go to Window > Package Manager, locate *High Definition RP* and update it to the latest version.
5. Done! Try playing the Assets/scenes/world_menu scene and creating a new world. Note that, the first time 
the world is rendered, it might look super weird for a few seconds while the pipeline fires up for the first time. 
The materials should load in momentarily.
