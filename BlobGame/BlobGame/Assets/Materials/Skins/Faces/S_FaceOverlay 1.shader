Shader "Custom/FaceOverlay"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,0,1)
        _FaceTex ("Face Texture", 2D) = "white" {}
        _FaceSize ("Face Size", Float) = 2.0
        _FaceScale ("Face Scale", Range(0.2, 2.0)) = 1.0
        _FaceRadius ("Face Radius", Range(0.15, 1.2)) = 0.6
        _FaceOffsetX ("Face Offset X", Range(-0.5, 0.5)) = 0.0
        _FaceOffsetY ("Face Offset Y", Range(-0.5, 0.5)) = 0.08
        _FacePitch ("Face Pitch Toward Move", Range(0,1)) = 0.55
        _FaceStrength ("Face Strength", Range(0,1)) = 1.0
        _FaceDirOS ("Face Dir (World XZ)", Vector) = (0,0,1,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard

        sampler2D _FaceTex;
        fixed4 _BaseColor;
        float _FaceSize;
        float _FaceScale;
        float _FaceRadius;
        float _FaceOffsetX;
        float _FaceOffsetY;
        float _FacePitch;
        float _FaceStrength;
        float4 _FaceDirOS;

        struct Input
        {
            float3 worldNormal;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            const float TOP_START = 0.0;
            const float TOP_FEATHER = 0.001;
            const float FRONT_FEATHER = 0.001;

            // Stable world-space basis for upright blobs.
            float3 nWS = normalize(IN.worldNormal);
            float3 faceForwardWS = normalize(float3(_FaceDirOS.x, 0, _FaceDirOS.z));
            if (dot(faceForwardWS, faceForwardWS) < 1e-5) faceForwardWS = float3(0, 0, 1);
            float3 upWS = float3(0, 1, 0);
            float3 rightWS = normalize(cross(upWS, faceForwardWS));
            if (dot(rightWS, rightWS) < 1e-5) rightWS = float3(1, 0, 0);

            // Tilt the decal center forward from zenith, avoiding heavy UV offset distortion.
            float3 centerWS = normalize(lerp(upWS, faceForwardWS, saturate(_FacePitch)));
            float3 decalRightWS = normalize(cross(upWS, centerWS));
            if (dot(decalRightWS, decalRightWS) < 1e-5) decalRightWS = rightWS;
            float3 decalUpWS = -normalize(cross(centerWS, decalRightWS));

            // Centered top-decal projection in local frame:
            // U = right/left, V = forward/back (movement aligned).
            float2 plane;
            plane.x = dot(nWS, decalRightWS);
            plane.y = dot(nWS, decalUpWS);

            float radius = max(_FaceRadius, 1e-4);
            float2 uv = plane / (2.0 * radius / max(_FaceScale, 1e-4)) + 0.5;
            uv += float2(_FaceOffsetX, _FaceOffsetY);
            // Texture V comes in inverted for this projection.
            uv.y = 1.0 - uv.y;

            fixed4 face = tex2D(_FaceTex, uv);

            // Top hemisphere only.
            float topMask = smoothstep(TOP_START, TOP_START + TOP_FEATHER, nWS.y);
            // Front hemisphere emphasis (still keeps full sprite readable).
            float frontDot = dot(nWS, faceForwardWS);
            float frontMask = smoothstep(-FRONT_FEATHER, FRONT_FEATHER, frontDot);
            float shapeMask = saturate(pow(saturate(frontMask), _FaceSize));
            // Keep decal concentrated around its tilted center.
            float capMask = smoothstep(0.0, 0.18, dot(nWS, centerWS));
            // Keep overlay strictly inside decal rectangle bounds.
            float inU = step(0.0, uv.x) * step(uv.x, 1.0);
            float inV = step(0.0, uv.y) * step(uv.y, 1.0);
            float boundsMask = inU * inV;
            float mask = topMask * shapeMask * capMask * boundsMask;

            fixed3 finalColor = lerp(_BaseColor.rgb, face.rgb, face.a * mask * _FaceStrength);

            o.Albedo = finalColor;
        }
        ENDCG
    }
}