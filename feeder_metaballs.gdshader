shader_type canvas_item;

uniform vec4[10] metaballs;
uniform float threshold = 0.012;
uniform float meniscus = 0.00025;

uniform vec4 color = vec4(0.478431, 0.0901961, 0, 0);

void fragment() {

	vec2 pos = FRAGCOORD.xy - 0.5 / SCREEN_PIXEL_SIZE;

	float sum = 0.0;
	for (int i = 0; i < 10; i++) {
		vec4 metaball = metaballs[i];
		sum += metaball.z / pow(length(pos - metaball.xy), 2);
	}

	COLOR = vec4(color.rgb, smoothstep(threshold - meniscus, threshold + meniscus, sum));
}
