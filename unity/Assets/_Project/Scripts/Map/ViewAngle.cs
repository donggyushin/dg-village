using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 고정 카메라의 각도. 스프라이트 · 벽 · 빛무리가 카메라를 정면으로 보도록 같은 회전(Billboard)을 쓰고,
    /// 이동 입력(화면 기준)을 월드 방향으로 돌릴 때도 쓴다.
    /// 기본 = 정면 (X 35°, 마을). 쿼터뷰 실험(MapTest)은 씬의 ViewAngleOverride 가 바꾼다.
    /// Domain Reload 가 꺼져 있어 Play 마다 기본값으로 되돌린다.
    /// </summary>
    public static class ViewAngle
    {
        private static readonly int TexelAxesId = Shader.PropertyToID("_DgTexelAxes");

        public const float DefaultPitch = 35f;
        public const float DefaultYaw = 0f;

        public static float Pitch { get; private set; } = DefaultPitch;
        public static float Yaw { get; private set; } = DefaultYaw;

        /// <summary>카메라를 정면으로 보는 세운 스프라이트의 회전</summary>
        public static Quaternion Billboard => Quaternion.Euler(Pitch, Yaw, 0f);

        public static bool IsQuarterView => !Mathf.Approximately(Yaw, 0f);

        public static void Set(float pitch, float yaw)
        {
            Pitch = pitch;
            Yaw = yaw;
            // 조명 디더 격자(DgLighting.hlsl)를 화면 픽셀 방향으로. 정면 카메라는 예전 그대로 월드 XZ (0 = 끔)
            Vector4 axes = Vector4.zero;
            if (IsQuarterView)
            {
                float yawRad = yaw * Mathf.Deg2Rad;
                float lift = Mathf.Sin(pitch * Mathf.Deg2Rad);
                axes = new Vector4(Mathf.Cos(yawRad), -Mathf.Sin(yawRad), Mathf.Sin(yawRad) * lift, Mathf.Cos(yawRad) * lift);
            }
            Shader.SetGlobalVector(TexelAxesId, axes);
        }

        /// <summary>화면 기준 입력(x = 오른쪽, y = 위)을 바닥(XZ) 위 방향으로</summary>
        public static Vector3 ScreenToGround(Vector2 input) =>
            Quaternion.Euler(0f, Yaw, 0f) * new Vector3(input.x, 0f, input.y);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Set(DefaultPitch, DefaultYaw);
    }
}
