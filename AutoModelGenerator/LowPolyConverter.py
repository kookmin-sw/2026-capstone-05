import bpy
import os
import math

# ==========================================
# ⚙️ 사용자 설정 (경로를 본인 PC에 맞게 수정하세요)
# ==========================================
INPUT_OBJ = "D:/raw_model.obj" 
OUTPUT_DIR = "D:"
TEXTURE_SIZE = 1024 
DECIMATE_RATIO = 0.025 
# ==========================================

def setup_environment():
    """안전한 초기화 및 GPU 렌더링 세팅 (컨텍스트 에러 방지)"""
    
    # 1. 팩토리 리셋 대신 오브젝트와 재질만 깔끔하게 삭제
    for obj in bpy.data.objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for mat in bpy.data.materials:
        bpy.data.materials.remove(mat, do_unlink=True)
        
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    
    prefs = bpy.context.preferences
    cprefs = prefs.addons['cycles'].preferences
    cprefs.compute_device_type = 'CUDA'
    cprefs.get_devices()
    
    for device in cprefs.devices:
        if device.type == 'CUDA':
            device.use = True
            
    scene.cycles.device = 'GPU'
    scene.cycles.bake_type = 'DIFFUSE'
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 16

def process_model():
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)

    # 🌟 [핵심] 현재 화면 중 3D 뷰포트를 찾아서 해당 컨텍스트로 강제 실행 (Blender 4.0+ 필수)
    area = next((a for a in bpy.context.screen.areas if a.type == 'VIEW_3D'), None)
    region = next((r for r in area.regions if r.type == 'WINDOW'), None) if area else None

    if not area:
        print("Error: 3D Viewport not found! 화면에 3D 뷰포트 창이 하나는 띄워져 있어야 합니다.")
        return

    # with 문을 통해 3D 뷰포트 환경인 것처럼 속이고 아래 작업들을 일괄 실행합니다.
    with bpy.context.temp_override(area=area, region=region):
        
        # 1. 모델 임포트
        bpy.ops.wm.obj_import(filepath=INPUT_OBJ)
        obj = bpy.context.selected_objects[0]
        bpy.context.view_layer.objects.active = obj

        obj.rotation_euler[2] = math.radians(180) # Z축 기준 180도 회전
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False) # 회전값 적용(Apply)

        # 1.5 Decimate 및 Flat Shading
        print(f"Applying Decimate Modifier (Ratio: {DECIMATE_RATIO})...")
        mod = obj.modifiers.new(name="Decimate", type='DECIMATE')
        mod.ratio = DECIMATE_RATIO
        
        bpy.ops.object.modifier_apply(modifier="Decimate")
        bpy.ops.object.shade_flat()
        print("Optimization and Flat Shading applied.")

        # 2. Smart UV Project
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.01)
        bpy.ops.object.mode_set(mode='OBJECT')

        # 3. 재질(Material) 및 노드 세팅
        mat = bpy.data.materials.new(name="Baked_Mat")
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        nodes.clear()

        node_vcol = nodes.new(type="ShaderNodeVertexColor")
        node_vcol.layer_name = obj.data.color_attributes[0].name if obj.data.color_attributes else "Color"
        
        node_bsdf = nodes.new(type="ShaderNodeBsdfPrincipled")
        links.new(node_vcol.outputs['Color'], node_bsdf.inputs['Base Color'])
        
        node_output = nodes.new(type="ShaderNodeOutputMaterial")
        links.new(node_bsdf.outputs['BSDF'], node_output.inputs['Surface'])

        img_name = "baked_texture"
        img = bpy.data.images.new(img_name, width=TEXTURE_SIZE, height=TEXTURE_SIZE)
        node_img = nodes.new(type="ShaderNodeTexImage")
        node_img.image = img
        node_img.interpolation = 'Closest'
        nodes.active = node_img

        obj.data.materials.append(mat)

        # 4. 베이킹 실행 (GPU 사용)
        print("Baking started...")
        bpy.ops.object.bake(type='DIFFUSE', save_mode='EXTERNAL')
        print("Baking finished!")

        # 5. 구워진 텍스처 이미지 저장
        tex_path = os.path.join(OUTPUT_DIR, f"{img_name}.png")
        img.filepath_raw = tex_path
        img.file_format = 'PNG'
        img.save()

        # 6. 구워진 이미지를 재질에 연결
        links.new(node_img.outputs['Color'], node_bsdf.inputs['Base Color'])

        # 7. FBX로 익스포트
        fbx_path = os.path.join(OUTPUT_DIR, "final_lowpoly_model.fbx")
        bpy.ops.export_scene.fbx(
            filepath=fbx_path,
            use_selection=True,
            path_mode='COPY', 
            embed_textures=True
        )
        print(f"All done! Saved to: {OUTPUT_DIR}")

# 실행
setup_environment()
process_model()