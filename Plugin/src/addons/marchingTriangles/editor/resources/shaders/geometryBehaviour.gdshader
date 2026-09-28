shader_type spatial;
render_mode diffuse_toon, depth_draw_opaque, cull_disabled;

varying vec3 data;

void vertex() {
	data = CUSTOM3.rgb;
}

void fragment(){
	ALBEDO=data;
}