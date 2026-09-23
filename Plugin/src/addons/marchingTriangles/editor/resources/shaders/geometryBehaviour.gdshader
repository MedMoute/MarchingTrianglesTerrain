shader_type spatial;
render_mode diffuse_toon, depth_draw_opaque, cull_disabled;

varying vec3 data;

void vertex() {
	data=vec3(CUSTOM3.r*128.,0,0);
}

void fragment(){
	ALBEDO=data;
}