# Don't get lost
A procedurally generated video game.
## Setup
To setup a unity project for development, follow these steps:
1. Create a new unity project using the "High Definition RP" template.
2. Replace the Assets/ folder (in its entirety) with a clone of this repository. <br>
    Linux steps:
    ~~~~
    $ cd /path/to/project/folder
    $ git clone https://github.com/miicck/dont-get-lost
    $ rm -r Assets
    $ mv dont-get-lost Assets
    ~~~~
 
3. Load the scene Assets/scenes/project_setup, select the object called "setup" in the heirarchy and click "Run setup" in the inspector.
4. In Edit > Project settings > HDRP Default Settings, ensure both "Default Volume Profile Asset" 
and "LookDev Volume Profile Asset" are set to Assets/pipeline/global_volume.
5. Load Assets/scenes/main, go to Lighting settings and, under "Environment" ensure "Profile" is set to 
Assets/pipeline/global_volume and the "Static Lighting Sky" dropdown is set to GradientSky.
6. Add Assets/scenes/world_menu and Assets/scenes/main (in that order!) to File > Build Settings > Scenes In Build.
7. Done! Try playing the Assets/scenes/world_menu scene and creating a new world. Note that, the first time 
the world is rendered, it might look super weird for a few seconds while the pipeline fires up for the first time. 
The materials should load in momentarily.
