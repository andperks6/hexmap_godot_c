shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

#include "hex_cell_data.gdshaderinc"
#include "water.gdshaderinc"

uniform vec4 base_color : source_color;
uniform sampler2D noise_texture;
uniform sampler2D normal_map_a : hint_normal, filter_linear_mipmap;
uniform sampler2D normal_map_b : hint_normal, filter_linear_mipmap;
uniform sampler2D depth_texture : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

// River settings
uniform float flow_speed = 1.0;
uniform float refraction = 0.05;
uniform float transparency = 0.9;
uniform float river_depth = 0.5;
uniform float whitewater_amount = 0.3;

varying float visibility;
varying float explored;
varying vec3 world_position;
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
    // Get flow direction from UV2
    vec2 flow_dir = UV2;
    
    // Flow effect using two noise layers
    vec2 flow_uv = world_position.xz * 0.5;
    float river_noise1 = compute_river(vec2(flow_dir.x, flow_uv.y), noise_texture);
    
    // Second flow layer offset and faster
    vec2 flow_uv2 = flow_uv * 1.3;
    float river_noise2 = compute_river(vec2(flow_dir.x, flow_uv2.y), noise_texture);
    
    // Combine flow layers
    float flow = (river_noise1 * 0.65 + river_noise2 * 0.35) * 0.3;
    
    // Calculate flow vector based on UV2
    float flow_angle = (flow_dir.x * 2.0 - 1.0) * 3.14159;
    vec2 flow_vector = vec2(sin(flow_angle), cos(flow_angle)) * flow_speed;
    
    // Simple foam based on flow direction
    vec2 foam_uv = flow_uv;
    foam_uv += flow_vector * TIME;
    float foam = texture(noise_texture, foam_uv).x;
    foam = smoothstep(0.6, 0.8, foam) * whitewater_amount;
    
    // Normal mapping following flow direction
    vec2 uv = world_position.xz * 0.4;
    vec2 uv_a = uv + flow_vector * TIME * 0.5;
    vec2 uv_b = uv * 1.4 + flow_vector * TIME * 0.3;
    
    vec3 normal_map = texture(normal_map_a, uv_a).rgb * 0.7;
    normal_map += texture(normal_map_b, uv_b).rgb * 0.3;

    // Refraction
    vec3 ref_normal = normal_map * 2.0 - 1.0;
    ref_normal = normalize(vertex_tangent * ref_normal.x + vertex_binormal * ref_normal.y + vertex_normal * ref_normal.z);
    vec2 ref_uv = SCREEN_UV + (ref_normal.xy * refraction) / vertex_height;
    
    // Depth-based effects
    float depth_raw = texture(depth_texture, ref_uv).r * 2.0 - 1.0;
    float depth = PROJECTION_MATRIX[3][2] / (depth_raw + PROJECTION_MATRIX[2][2]);
    float depth_blend = exp((depth + VERTEX.z - river_depth) * -2.0);
    depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);
    
    // Color blending with refraction
    vec3 screen_color = textureLod(screen_texture, ref_uv, depth_blend * 2.0).rgb;
    vec3 water_color = base_color.rgb + flow;
    vec3 final_color = mix(screen_color * water_color, water_color * 0.6, depth_blend * 0.6);
    
    // Add foam
    final_color = mix(final_color, vec3(1.0), foam);
    
    vec4 c = vec4(final_color, transparency);
    c = clamp(c, 0.0, 1.0);
    
    ALBEDO = c.rgb * visibility;
    ALPHA = c.a * explored;
    METALLIC = mix(0.1, 0.0, foam);
    ROUGHNESS = mix(0.2, 0.8, foam);
    SPECULAR = mix(0.4 + depth_blend * 0.2, 0.1, foam);
    NORMAL_MAP = normal_map;
    NORMAL_MAP_DEPTH = 0.8;
    AO = explored;
}
