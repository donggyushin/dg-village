using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 이 씬에서만 카메라 각도(ViewAngle)를 바꾼다 — 쿼터뷰 실험용 (MapTest).
    /// 다른 스크립트보다 먼저 켜져야 바닥 · 빛무리가 이 각도로 만들어진다.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-1000)]
    public class ViewAngleOverride : MonoBehaviour
    {
        [SerializeField] private float _pitch = 30f;
        [SerializeField] private float _yaw = 45f;

        private void OnEnable() => ViewAngle.Set(_pitch, _yaw);

        private void OnDisable() => ViewAngle.Set(ViewAngle.DefaultPitch, ViewAngle.DefaultYaw);
    }
}
