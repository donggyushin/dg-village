using UnityEngine;

public class Occluder : MonoBehaviour
{
    private const string LayerName = "Occluder";
    [SerializeField] private float _fadedAlpha = 0.35f;
    [SerializeField] private float _fadeSpeed = 4f;

    private MeshRenderer _renderer;
    private Material _originalMaterial;
    private Material _fadeMaterial;
    private bool _occluding;
    private float _alpha = 1f;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _originalMaterial = _renderer.sharedMaterial;

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        var colliderObject = new GameObject("OccluderCollider");
        colliderObject.layer = LayerMask.NameToLayer(LayerName);
        colliderObject.transform.SetParent(transform, false);

        // 그림 메시 모양 그대로 판을 만든다 (상자로 감싸면 V자 건물 앞 빈 공간까지 잡힘).
        var meshCollider = colliderObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }
    public void SetOccluding(bool occluding, Material fadeTemplate)
    {
        _occluding = occluding;

        if (occluding && _fadeMaterial == null)
        {
            _fadeMaterial = new Material(fadeTemplate);
            _fadeMaterial.SetTexture("_BaseMap", _originalMaterial.GetTexture("_BaseMap"));
        }
    }

    private void Update()
    {
        if (_fadeMaterial == null) return;

        float target = _occluding ? _fadedAlpha : 1f;
        _alpha = Mathf.MoveTowards(_alpha, target, _fadeSpeed * Time.deltaTime);

        if (_alpha >= 1f)
        {
            _renderer.sharedMaterial = _originalMaterial;
            return;
        }

        Color color = _originalMaterial.GetColor("_BaseColor");
        color.a *= _alpha;
        _fadeMaterial.SetColor("_BaseColor", color);
        _renderer.sharedMaterial = _fadeMaterial;
    }

    private void OnDestroy()
    {
        if (_fadeMaterial != null) Destroy(_fadeMaterial);
    }
}
