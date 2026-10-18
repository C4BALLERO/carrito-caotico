"""Recorta los personajes KayKit para Unity: cada personaje sin armas ni animaciones y un FBX
aparte con el esqueleto y las animaciones elegidas (todos comparten el mismo rig).

Necesita Blender como módulo de Python (pip install bpy, Python 3.11). Ejemplo:
  SRC=KayKit-Character-Pack-Adventures-1.0/addons/kaykit_character_pack_adventures/Characters/fbx \
  OUT=Assets/_CarritoCaotico/06_Models/NPC/Aventureros CHARS=Knight,Barbarian,Mage,Rogue,RogueHooded \
  ANIM_FROM=Knight python recortar_kaykit.py
(NPC_Animaciones.fbx se escribe en OUT: muévelo a 06_Models/NPC.)
"""
import bpy, os

SRC = os.environ["SRC"]          # carpeta con los FBX originales
OUT = os.environ["OUT"]
CHARS = os.environ["CHARS"].split(",")
ANIM_FROM = os.environ.get("ANIM_FROM")
KEEP = ["Idle", "Walking_A", "PickUp", "Cheer"]

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    return next(o for o in bpy.data.objects if o.type == 'ARMATURE')

def rest_pose(rig):
    if rig.animation_data: rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)

def export(path, anim):
    bpy.ops.export_scene.fbx(filepath=path, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        bake_anim=anim, bake_anim_use_all_actions=anim, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5, embed_textures=False,
        path_mode='STRIP', use_armature_deform_only=False, apply_scale_options='FBX_SCALE_UNITS')

if ANIM_FROM:
    rig = load(os.path.join(SRC, ANIM_FROM + ".fbx"))
    for o in [o for o in bpy.data.objects if o.type != 'ARMATURE']: bpy.data.objects.remove(o)
    for a in list(bpy.data.actions):
        short = a.name.split("|")[-1]
        if short in KEEP: a.name = short
        else: bpy.data.actions.remove(a)
    rest_pose(rig)
    export(os.path.join(OUT, "NPC_Animaciones.fbx"), True)
    print("ANIMS", sorted(a.name for a in bpy.data.actions))

for name in CHARS:
    if not name: continue
    rig = load(os.path.join(SRC, name + ".fbx"))
    for o in [o for o in bpy.data.objects if o.parent_bone.startswith("handslot")]:
        bpy.data.objects.remove(o)
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    rest_pose(rig)
    export(os.path.join(OUT, name + ".fbx"), False)
    print("CHAR", name, [o.name for o in bpy.data.objects])
