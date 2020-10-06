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
    $ git remote add origin https://github.com/miicck/dont-get-lost
    $ git fetch
    $ git checkout -t origin/master
    Re-open the unity editor/load the project
    ~~~~
    If you're working with a newer version of unity than the last commit in this project, unity may ask to do a 
    reimport. This should be fine. Note that using an older version than the last commit is not supported - update your engine.
 
3. (optional, recommended) Go to Window > Package Manager, locate *High Definition RP* and update it to the latest version (it may already be the latest version).
4. Done! Try playing the Assets/scenes/world_menu scene and creating a new world. Note that, the first time 
the world is rendered, it might look super weird for a few seconds while the shaders compile for the first time.

Feel free to fire off merge requests with any changes you've made!
