shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

#include "hex_cell_data.gdshaderinc"
#include "water.gdshaderinc"

uniform sampler2D depth_texture : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

uniform vec4 base_color : source_color;
uniform sampler2D noise_texture;

// Surface settings
uniform sampler2D normal_map_a : hint_normal, filter_linear_mipmap;
uniform sampler2D normal_map_b : hint_normal, filter_linear_mipmap;
uniform vec2 sampler_scale = vec2(0.25, 0.25);
uniform float refraction = 0.075;
uniform float transparency = 0.92;

// Depth settings
uniform vec4 color_deep : source_color = vec4(0.0, 0.2, 0.3, 0.7);
uniform float beers_law = 2.0;
uniform float depth_offset = -0.75;

varying vec3 world_position;
varying float visibility;
varying float explored;
varying float vertex_height;
varying vec3 vertex_normal;
varying vec3 vertex_binormal;
varying vec3 vertex_tangent;

void vertex()
{
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    vec4 cell0 = GetCellData(CUSTOM0.xyz, 0);
    vec4 cell1 = GetCellData(CUSTOM0.xyz, 1);
    vec4 cell2 = GetCellData(CUSTOM0.xyz, 2);

    visibility = (cell0.x * COLOR.x) + (cell1.x * COLOR.y) + (cell2.x * COLOR.z);
    visibility = mix(0.25, 1.0, visibility);
    explored = (cell0.y * COLOR.x) + (cell1.y * COLOR.y) + (cell2.y * COLOR.z);

    // Apply wave displacement with noise texture
    vec3 wave_offset = compute_waves_new(world_position.xz, VERTEX, noise_texture);
    VERTEX += wave_offset;

    // Store data for fragment shader
    vertex_height = (PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
    vertex_tangent = TANGENT;
    vertex_binormal = BINORMAL;
    vertex_normal = NORMAL;
}

void fragment()
{
    // Adjust UV coordinates for hex grid
    vec2 hex_uv = world_position.xz;
    hex_uv.x += sin(hex_uv.y * 0.5) * 0.1;

    // Basic wave effect with hex grid adjustment
    float waves = compute_waves(hex_uv, noise_texture);

    // Normal mapping with hex grid consideration
    vec2 uv = hex_uv * sampler_scale;

    // Create two rotated UV sets for normal maps
    float angle = 0.5;
    vec2 uv_a = uv - vec2(TIME * 0.1);
    vec2 uv_b = vec2(
        uv.x * cos(angle) - uv.y * sin(angle),
        uv.x * sin(angle) + uv.y * cos(angle)
    ) + vec2(TIME * 0.08);

    vec3 normal_map = texture(normal_map_a, uv_a).rgb * 0.7;
    normal_map += texture(normal_map_b, uv_b).rgb * 0.3;

    // Add fine detail normal variation based on noise
    vec2 detail_uv = hex_uv * 4.0 + TIME * 0.1;
    vec3 detail_normal = texture(noise_texture, detail_uv).rgb * 2.0 - 1.0;
    normal_map = mix(normal_map, detail_normal, 0.1);

    // Refraction with hex grid adjustment
    vec3 ref_normal = normal_map * 2.0 - 1.0;
    ref_normal = normalize(vertex_tangent * ref_normal.x + vertex_binormal * ref_normal.y + vertex_normal * ref_normal.z);
    vec2 ref_uv = SCREEN_UV + (ref_normal.xy * refraction) / vertex_height;

    // Depth calculation
    float depth_raw = texture(depth_texture, ref_uv).r * 2.0 - 1.0;
    float depth = PROJECTION_MATRIX[3][2] / (depth_raw + PROJECTION_MATRIX[2][2]);

    float depth_blend = exp((depth + VERTEX.z + depth_offset) * -beers_law);
    depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);
    float depth_blend_pow = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

    // Color blending with improved depth transition
    vec3 screen_color = textureLod(screen_texture, ref_uv, depth_blend_pow * 2.5).rgb;
    vec3 water_color = mix(base_color.rgb, color_deep.rgb, depth_blend_pow);
    vec3 final_color = mix(screen_color * water_color, water_color * 0.25, depth_blend_pow * 0.5);

    // Apply waves with smoother blending
    final_color += waves * 0.1 * (1.0 - depth_blend_pow);

    // Edge fade to help with tile seams
    float edge_fade = 1.0 - smoothstep(0.4, 0.5, abs(UV.x - 0.5));
    edge_fade *= 1.0 - smoothstep(0.4, 0.5, abs(UV.y - 0.5));

    ALBEDO = final_color * visibility;
    ALPHA  = (mix(transparency, 1.0, depth_blend_pow)) * explored;
    METALLIC = 0.1;
    ROUGHNESS = 0.2;
    SPECULAR = 0.2 + depth_blend_pow * 0.4;
    NORMAL_MAP = normal_map;
    NORMAL_MAP_DEPTH = 1.25;
    AO = explored;
}
