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

// Flow settings
uniform float flow_speed = 0.5;
uniform float whitewater_amount = 0.2;

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

    visibility = (cell0.x * COLOR.x) + (cell1.x * COLOR.y);
    visibility = mix(0.25, 1.0, visibility);
    explored = (cell0.y * COLOR.x) + (cell1.y * COLOR.y);
    
    vertex_height = (PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
    vertex_tangent = TANGENT;
    vertex_binormal = BINORMAL;
    vertex_normal = NORMAL;
}

void fragment()
{
    float shore = UV.y;
    float blend = UV.x; // Blend factor between shore and river
    
    // Compute shore water effects
    float foam = compute_foam(shore, world_position.xz, noise_texture);
    float waves = compute_waves(world_position.xz, noise_texture);
    float shore_water = max(foam, waves);
    
    // Improved river effect with two flow layers
    vec2 flow_uv = UV2 * 0.5;
    float river_noise1 = compute_river(flow_uv, noise_texture);
    
    vec2 flow_uv2 = flow_uv * 1.3;
    flow_uv2.y += TIME * flow_speed * 1.5;
    float river_noise2 = compute_river(flow_uv2, noise_texture);
    
    float river_flow = (river_noise1 * 0.65 + river_noise2 * 0.35) * 0.3;
    
    // Simple foam for river part
    vec2 foam_uv = flow_uv;
    foam_uv.y -= TIME * flow_speed;
    float river_foam = texture(noise_texture, foam_uv).x;
    river_foam = smoothstep(0.6, 0.8, river_foam) * whitewater_amount;
    
    // Normal mapping with flow direction influence
    vec2 uv = world_position.xz * sampler_scale;
    vec2 flow_offset = vec2(0.0, TIME * flow_speed);
    vec2 river_uv = UV2 * sampler_scale - flow_offset;
    
    // Blend between shore and river UVs
    vec2 final_uv = mix(uv, river_uv, blend);
    vec2 uv_a = final_uv - vec2(TIME * 0.1);
    vec2 uv_b = final_uv + vec2(TIME * 0.08);
    
    vec3 normal_map = texture(normal_map_a, uv_a).rgb * 0.7;
    normal_map += texture(normal_map_b, uv_b).rgb * 0.3;

    // Refraction
    vec3 ref_normal = normal_map * 2.0 - 1.0;
    ref_normal = normalize(vertex_tangent * ref_normal.x + vertex_binormal * ref_normal.y + vertex_normal * ref_normal.z);
    vec2 ref_uv = SCREEN_UV + (ref_normal.xy * refraction) / vertex_height;

    // Depth calculation
    float depth_raw = texture(depth_texture, ref_uv).r * 2.0 - 1.0;
    float depth = PROJECTION_MATRIX[3][2] / (depth_raw + PROJECTION_MATRIX[2][2]);
    
    float depth_blend = exp((depth + VERTEX.z + depth_offset) * -beers_law);
    depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);
    float depth_blend_pow = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

    // Color blending
    vec3 screen_color = textureLod(screen_texture, ref_uv, depth_blend_pow * 2.5).rgb;
    vec3 water_color = mix(base_color.rgb, color_deep.rgb, depth_blend_pow);
    vec3 final_color = mix(screen_color * water_color, water_color * 0.25, depth_blend_pow * 0.5);
    
    // Blend between shore and river effects
    float water_effect = mix(shore_water, river_flow + river_foam, blend);
    vec4 c = vec4(final_color, base_color.a);
    c += water_effect;
    c = clamp(c, 0.0, 1.0);
    
    ALBEDO = c.rgb * visibility;
    ALPHA = c.a * explored;
    METALLIC = mix(0.1, 0.0, water_effect);
    ROUGHNESS = mix(0.2, 0.8, water_effect);
    SPECULAR = 0.2 + depth_blend_pow * 0.4;
    NORMAL_MAP = normal_map;
    NORMAL_MAP_DEPTH = mix(1.25, 0.8, blend);
    AO = explored;
}
