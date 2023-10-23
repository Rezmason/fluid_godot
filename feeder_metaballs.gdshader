shader_type canvas_item;

uniform vec4[10] metaballs;
uniform vec4[3] metaballGroups;
uniform float threshold = 0.01;
uniform float meniscus = 0.00025;

uniform vec4 color = vec4(0.478431, 0.0901961, 0, 0);

void fragment() {

	vec2 pos = FRAGCOORD.xy - 0.5 / SCREEN_PIXEL_SIZE;

	float sums[3] = float[3](0.0, 0.0, 0.0);
	for (int i = 0; i < 10; i++) {
		vec4 metaball = metaballs[i];
		sums[int(metaball.w)] += metaball.z / pow(length(pos - metaball.xy) + 3.5, 2.0);
	}
	
	float value = 0.0;
	for (int i = 0; i < 3; i++) {
		vec4 metaballGroup = metaballGroups[i];
		float outsideEdge = sums[i] - threshold;
		float insideEdge = sums[i] - threshold - 0.0085;
		float insideOpacity = metaballGroup[0];
		value = max(value,
			smoothstep(-meniscus, +meniscus, outsideEdge) * 
			mix(1.0, insideOpacity, smoothstep(-meniscus, +meniscus, insideEdge))
		);
	}
	
	COLOR =  vec4(color.rgb, value);
}
