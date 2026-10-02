shader_type spatial;
render_mode diffuse_toon, depth_draw_opaque, cull_disabled;

mat3 getRotationMatrix(mat4 mv) {
    mat3 rot = mat3(mv);
    rot[0] = normalize(rot[0]);
    rot[1] = normalize(rot[1]);
    rot[2] = normalize(rot[2]);
    return rot;
}

	uniform vec3 WHITE = vec3(1,1,1);
	uniform vec3 BLACK = vec3(0,0,0);

varying vec3 c1;
varying vec3 c2;
varying float r;

// Extract pure rotation/orientation matrix (3x3)


void vertex() {
	mat3 rot = getRotationMatrix(VIEW_MATRIX);
	r = atan(rot[0].y)+PI/6.f;

	//Unpack Custom 3 in 2 values
	vec2 R= unpackHalf2x16(floatBitsToUint(CUSTOM3.r));
	vec2 G= unpackHalf2x16(floatBitsToUint(CUSTOM3.g));
	vec2 B=unpackHalf2x16(floatBitsToUint(CUSTOM3.b));
	
	c1=vec3(R.x,G.x,B.x);
	c2=vec3(R.y,G.y,B.y);

}



void fragment(){
    vec2 uv = VERTEX.xy/vec2(20,20);
    float stripeNum =40.;	
	
	float rotPosOrth = cos(r+PI/2.f)*uv.x+sin(r+PI/2.f)*uv.y;

	float dashXCondition = step(0.2,mod(rotPosOrth*stripeNum/sqrt(2),1.0));
	
	float rotPos = cos(r)*uv.x+sin(r)*uv.y;
	
    float stripe = step(
		0.6,
		mod(rotPos*stripeNum/sqrt(2),1.0));
		
	float res = stripe*dashXCondition;
		

	vec3 c = vec3(res) * c1 + vec3(1. - res) * c2;

	ALBEDO=c;
}