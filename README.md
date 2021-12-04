# Don't get lost
A procedurally generated video game.
## Developing
To setup a unity project for development, follow these steps:
1. Using the latest version of unity, create a new unity project using the *High Definition RP* template.
2. Replace the Assets/ and ProjectSettings/ folders (in their entirety) with a clone of this repository. <br>
    Linux steps:
    ~~~~
    Close the unity editor
    $ cd /path/to/project/folder
    $ rm -r Assets/ ProjectSettings/
    $ git init
    $ git remote add origin git@github.com:miicck/dont-get-lost.git
    $ git fetch
    $ git checkout -t origin/master
    Re-open the unity editor/load the project
    ~~~~
    - If you're working with a newer version of unity than the last commit in this project, unity may ask to do a 
    reimport. This should be fine. Note that using an older version than the last commit is not supported - update your engine.
    - It might also ask to disable the old input manager, which I still use. You should say no to this.
    - If it says that there are compilation errors, choose ignore. If they persist after the project loads, submit a bug report.
 
3. (optional, recommended) Go to Window > Package Manager, locate *High Definition RP* and update it to the latest version (it may already be the latest version).
4. <b> If you want steam-based features: </b> <br>
Download the Facepunch.Steamworks C# wrapper from https://github.com/Facepunch/Facepunch.Steamworks/releases (the latest .zip file, not the source code) and put the contents of the Unity folder       within the .zip into Assets/Plugins (create Assets/Plugins if needed). Make sure that FACEPUNCH_STEAMWORKS is #defined in the project settings. <br> <br>
<b> If you don't want steam-based features: </b> <br>
Make sure that FACEPUNCH_STEAMWORKS is not #defined in the project settings.
5. Done! Try playing the Assets/scenes/world_menu scene and creating a new world. Note that, the first time 
the world is rendered, it might look super weird for a few seconds while the shaders compile for the first time.

Feel free to fire off merge requests with any changes you've made!
