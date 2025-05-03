#include "hex_metrics.gdshaderinc"

// Wave settings
uniform float wave_speed = 0.5;
uniform vec4 wave_a = vec4(1.0, 1.0, 0.35, 3.0); // xy = Direction, z = Steepness, w = Length
uniform vec4 wave_b = vec4(1.0, 0.6, 0.30, 1.55); // xy = Direction, z = Steepness, w = Length
uniform vec4 wave_c = vec4(1.0, 1.3, 0.25, 0.9); // xy = Direction, z = Steepness, w = Length

// Modified wave function using noise for direction variation
vec4 wave(vec4 parameter, vec2 position, float time, inout vec3 tangent, inout vec3 binormal, sampler2D noise_texture)
{
    float wave_steepness = parameter.z;
    float wave_length = parameter.w;

    // Sample noise for direction variation
    vec2 noise_uv = position * 0.01 + time * 0.05;
    vec4 noise = texture(noise_texture, noise_uv);
    vec2 dir_variation = (noise.xy * 2.0 - 1.0) * 0.2; // Scale down variation

    // Modify direction with noise
    vec2 d = normalize(parameter.xy + dir_variation);

    float k = 2.0 * 3.14159265359 / wave_length;
    float c = sqrt(9.8 / k);
    float f = k * (dot(d, position) - c * time);
    float a = wave_steepness / k;

    // Add noise to amplitude
    float amp_variation = mix(0.8, 1.2, noise.z);
    a *= amp_variation;

    tangent += normalize(vec3(1.0-d.x * d.x * (wave_steepness * sin(f)), d.x * (wave_steepness * cos(f)), -d.x * d.y * (wave_steepness * sin(f))));
    binormal += normalize(vec3(-d.x * d.y * (wave_steepness * sin(f)), d.y * (wave_steepness * cos(f)), 1.0-d.y * d.y * (wave_steepness * sin(f))));

    return vec4(d.x * (a * cos(f)), a * sin(f) * 0.25, d.y * (a * cos(f)), 0.0);
}

float compute_river(vec2 river_uv, sampler2D noise_texture)
{
    // Get flow direction from UV.x (0-1 range to -1 to 1 range)
    float flow_angle = (river_uv.x * 2.0 - 1.0) * 3.14159;
    vec2 flow_dir = vec2(sin(flow_angle), cos(flow_angle));
    
    // Sample noise along flow direction
    vec2 uv = river_uv;
    vec2 offset = flow_dir * TIME * 0.25;
    
    vec4 noise1 = texture(noise_texture, uv + offset);
    vec4 noise2 = texture(noise_texture, uv * 1.4 + offset * 0.7);
    
    return mix(noise1.x, noise2.w, 0.5);
}

// Improved foam function with better hex grid handling
float compute_foam(float shore, vec2 world_xz, sampler2D noise_texture)
{
    shore = sqrt(shore) * 0.9;

    // Adjust UV coordinates for hex grid
    vec2 hex_uv = world_xz;
    float hex_scale = TILING_SCALE * 2.0;
    hex_uv.x += sin(hex_uv.y * 0.5) * 0.1; // Slight offset based on Y to break up grid pattern

    vec2 noise_uv = hex_uv + TIME * 0.25;
    vec4 noise = texture(noise_texture, noise_uv * hex_scale);

    float distortion1 = noise.x * (1.0 - shore);
    float foam1 = sin((shore + distortion1) * 10.0 - TIME);
    foam1 *= foam1 * shore;

    float distortion2 = noise.y * (1.0 - shore);
    float foam2 = sin((shore + distortion2) * 10.0 + TIME + 2.0);
    foam2 *= foam2 * 0.7;

    float foam = max(foam1, foam2) * shore;

    // Add fine detail noise
    vec2 detail_uv = hex_uv * 4.0 + TIME * 0.1;
    float detail = texture(noise_texture, detail_uv).z * 0.1;
    foam += detail * shore;

    return foam;
}

vec3 compute_waves_new(vec2 world_xz, vec3 vertex_position, sampler2D noise_texture)
{
    float time = TIME * wave_speed;

    vec3 tang = vec3(0.0, 0.0, 0.0);
    vec3 bin = vec3(0.0, 0.0, 0.0);

    // Add hex grid adjustment
    vec2 hex_pos = world_xz;
    hex_pos.x += sin(hex_pos.y * 0.5) * 0.1;

    vec4 wave_offset = vec4(0.0);
    wave_offset += wave(wave_a, hex_pos, time, tang, bin, noise_texture);
    wave_offset += wave(wave_b, hex_pos, time, tang, bin, noise_texture);
    wave_offset += wave(wave_c, hex_pos, time, tang, bin, noise_texture);

    // Add noise-based displacement
    vec2 noise_uv = world_xz * 0.05 + time * 0.02;
    vec4 noise = texture(noise_texture, noise_uv);
    wave_offset.xz += (noise.xy * 2.0 - 1.0) * 0.1;

    return wave_offset.xyz;
}

// Unified wave computation that combines both systems
float compute_waves(vec2 world_xz, sampler2D noise_texture)
{
    // Adjust UV coordinates for hex grid
    vec2 hex_uv = world_xz;
    hex_uv.x += sin(hex_uv.y * 0.5) * 0.1;

    vec2 uv1 = hex_uv;
    uv1.y += TIME * 0.25;
    vec4 noise1 = texture(noise_texture, uv1 * (3.0 * TILING_SCALE));

    vec2 uv2 = hex_uv;
    uv2.x += TIME * 0.25;
    vec4 noise2 = texture(noise_texture, uv2 * (3.0 * TILING_SCALE));

    float blend_wave = sin((hex_uv.x + hex_uv.y) * 0.1f + (noise1.z + noise2.z) + TIME);
    blend_wave *= blend_wave;

    float waves = mix(noise1.z, noise1.w, blend_wave) + mix(noise2.z, noise2.y, blend_wave);
    waves = smoothstep(0.75, 2.0, waves);

    // Add fine detail noise
    vec2 detail_uv = hex_uv * 5.0 + TIME * 0.1;
    float detail = texture(noise_texture, detail_uv).z * 0.1;
    waves += detail;

    return waves;
}
