# Hotelier facial authoring copy

`HotelierFacePreview.blend` retains the Tripo hotelier body positions and UV layout and adds thirteen facial Shape Keys. Only the mouth cut and oral parts belong to this copy. The service skeleton, original texture and original prefab remain untouched. Unity keeps the source bone weights, bind poses and corrective channels.

The source coordinate export is `HotelierSourceSurface.json`. The authoring recipe reuses `../CafeFemaleFace/create_face_shapes.py` and its mouth construction with hotelier landmarks. This is an authored facial profile, not a universal topology transfer.

Run Blender with `--background --factory-startup --python ../CafeFemaleFace/create_face_shapes.py -- --surface HotelierSourceSurface.json --texture <original Material_0_color.png> --output <fresh directory> --name HotelierFace --offset -.04 -.043 -.028 --eye-centers .065 -.151 --eye-height .554 --eye-radius .043 .039 --skip-render`. The offset is an authoring coordinate translation, removed before export. The resulting mouth texture is renamed HotelierMouthColor.png in the JSON and project.

Unity's HotelierFaceBuilder creates a new nested prefab copy and uses the measured mesh height to set its wrapper to 1.7 m. It preserves the original nested model, Avatar and Animator Controller. Subsequent builds do not overwrite the authored copy. Edit the copy in Blender and re-check the full facial poses before replacing exported data.

The final visual reference is the Unity render, including explicit blink normal offsets. Blender's shading may differ.
