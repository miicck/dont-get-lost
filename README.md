# Don't get lost
An open-source (GPL-3 or later) procedurally generated video game. Steam page: https://store.steampowered.com/app/1442360/Dont_get_lost/

![](Assets/pictures/header_capsule.png?raw=true "Title")


https://user-images.githubusercontent.com/8690175/235303997-36b86f85-c29f-4e2a-8779-5ed61af1a8de.mp4


## Developing
To setup a unity project for development, follow these steps:
1. Using the latest version of unity, create a new unity project using the *High Definition Render Pipeline* (3D HDRP) template.
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
    - If it refuses to checkout origin/master because it would overwrite local files, delete the local file(s) mentioned and try again.
    - If you're working with a newer version of unity than the last commit in this project, unity may ask to do a 
    reimport. This should be fine. Note that using an older version than the last commit is not supported - update your engine.
    - It might also ask to disable the old input manager, which is still used. You should say no to this.
    - If it says that there are compilation errors, choose ignore. If they persist after the project loads, submit a bug report.
 
3. Done! Try playing the Assets/scenes/world_menu scene and creating a new world. Note that, the first time 
the world is rendered, it might look super weird for a few seconds while the shaders compile for the first time.

Feel free to fire off merge requests with any fixes you've made!
