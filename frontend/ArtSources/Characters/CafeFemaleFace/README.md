# Cafe female face authoring copy

This directory contains a face-only study of the Tripo cafe female. The source NPC and service rig remain unchanged.

- `CafeFemaleFacePreview.blend`: editable Basis plus thirteen body shape keys (nine speech channels and four expression channels). Lips, oral interior, teeth, tongue and closed-lid contours are separate; this is not a replacement body rig.
- `CafeFemaleSourceSurface.json`: exact source Unity mesh positions, UVs, normals and rig weight data used to identify the source topology.
- `CafeFemaleFaceShapes.json`: vertex offsets, blink normal offsets and oral mesh data consumed by the Unity builder.
- `build_speaking_mouth.py`: replaces the copy's mouth surface with lip loops and an oral bowl. It bakes a separate local skin texture; the source texture remains untouched.
- `References/`: supplied speech-mouth targets; all ten reference mouths are implemented: REST/Basis, PP, AA, E, OH, OU, FF, TH, I and EU.
- `create_face_shapes.py`: local Blender authoring recipe for this face. Use a fresh output directory to protect artist edits. Another face requires its own landmarks and visual checks.

Run Blender in background with `--python create_face_shapes.py -- --surface CafeFemaleSourceSurface.json --texture <original BaristaColor.png> --output <new output directory>`.

See `docs/unity/cafe-female-face.md` for the Unity scene, control names and prototype limitations. No service API is involved in this workflow.

Unity applies explicit shape-normal offsets. Blender live Shape Key shading can differ; use the Unity preview scene as the final visual check.

`export_local_speech.ps1` runs installed Windows en-US TTS locally and exports a matched PCM WAV/phoneme JSON pair. Use Windows PowerShell 5.1 with a new `-OutputDirectory`; existing outputs are preserved. No service API is called. The Unity speech sandbox consumes this pair through `CharacterSpeechClip`. Version 2 includes SAPI phonemes (ay, iy, y, uw, etc.) converted from engine IDs, as well as the original viseme events for comparison. The export uses 16 kHz audio to keep Zira audio/event clocks aligned and rejects out-of-range timing.

The cafe dialogue regression voices in `Assets/Audio/Characters/CafeDialogue/` are locally generated Kokoro `af_heart` speech, not calls to a paid TTS service. `CoffeeChoiceSpeech.bytes` contains “Would you like coffee or tea?”; `CafeThanksSpeech.bytes` contains “Thank you for visiting. Your coffee will be ready in five minutes.” They are standard PCM WAV files with the worker's `a305` timing chunk, imported as TextAsset bytes so the offline review exercises the same decoder as downloaded NPC speech. They do not replace the original Windows speech recording or source character.

For another authored profile, optional `--name`, `--offset`, `--eye-centers`, `--eye-height`, `--eye-radius` arguments keep the same speech construction while adjusting measured facial landmarks. Run with `--factory-startup` for deterministic Blender node names. The offset is removed before shape positions and the Blender copy are saved. The hotelier example is in `../HotelierFace/README.md`; it does not reuse the cafe mesh or overwrite its assets.
