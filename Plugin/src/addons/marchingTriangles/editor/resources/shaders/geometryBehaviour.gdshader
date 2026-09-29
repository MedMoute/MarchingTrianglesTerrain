shader_type spatial;
render_mode diffuse_toon, depth_draw_opaque, cull_disabled;

varying vec3 color1;
varying vec3 color2;

void vertex() {
	//Unpack Custom 3 in 2 values
	vec2 R= unpackHalf2x16(floatBitsToUint(CUSTOM3.r));
	vec2 G= unpackHalf2x16(floatBitsToUint(CUSTOM3.g));
	vec2 B=unpackHalf2x16(floatBitsToUint(CUSTOM3.b));
	
	color1=vec3(R.x,G.x,B.x);
	color2=vec3(R.y,G.y,B.y);
	color1=vec3(0.f,1.f,0.f);
	color2=vec3(1.f,0.f,0.f);
}
float new_step(float a, float b)
{
    if (a < b) return 1.;
    else return 0.;
}
void fragment(){
    vec2 uv = VERTEX.xy/vec2(20,20);

    float stripeNum = 20.;
	
    float stripe = step(0.5,mod((uv.x+uv.y)*stripeNum/2., 1.0));

    vec3 c = vec3(stripe) * color1 + vec3(1. - stripe) * color2;

	ALBEDO=c;
}