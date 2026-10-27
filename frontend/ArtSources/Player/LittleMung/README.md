# Little Mung source assets

- export_motions.py reads original Mixamo FBX files and exports their actual rest skeleton plus baked motion copies in one coordinate system. Blender 5.2 is used. See docs/unity/little-mung-player.md for the command.
- LittleMung_Player_Ankle_Adjusted.blend is an editable character rig candidate. The user's left terminal toe point is retained; ankle-to-forefoot connectivity and the corresponding right foot are aligned. Mesh vertices and other user edits are retained.
- LittleMung_Player_Ankle_Adjusted.fbx is an exchange copy of that candidate. It is not automatically applied to the Unity sandbox.
- Source Mixamo usage conditions are in frontend/UnityClient/Assets/ThirdParty/CREDITS.md.

Open the blend file in Edit Mode to change the rest skeleton. Bone Collections separate body, arms, fingers and end bones. Changing bone names, hierarchy, or adding bones requires rechecking skin weights and Unity Humanoid mapping.

- export_player.py exports the edited blend into a separate Unity FBX with FBX_SCALE_ALL. Run Blender in background with --python export_player.py -- --source <blend> --output <new fbx>. Existing outputs are rejected. The Unity AdjustedRig model and its dedicated sandbox use this export; the earlier exchange FBX remains unchanged.
