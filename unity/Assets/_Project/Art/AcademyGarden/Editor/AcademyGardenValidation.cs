using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class AcademyGardenValidation
{
    // Isolated physics scene: exercise the real saved fence shapes without starting login/network code.
    public static string Run()
    {
        var garden=GameObject.Find("18 • Academy north garden");
        var fence=garden.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Fence collision").ToArray();
        var sourceGround=garden.GetComponentInChildren<MeshCollider>();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var physics=scene.GetPhysicsScene();
        int runs=0,escapes=0;
        try {
            var floor=new GameObject("Test lawn");SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.SetPositionAndRotation(sourceGround.transform.position,sourceGround.transform.rotation);floor.transform.localScale=sourceGround.transform.lossyScale;
            floor.AddComponent<MeshCollider>().sharedMesh=sourceGround.sharedMesh;
            foreach(var source in fence) {
                var g=new GameObject("Test fence");SceneManager.MoveGameObjectToScene(g,scene);
                g.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);g.transform.localScale=source.transform.lossyScale;
                var c=g.AddComponent<BoxCollider>();c.center=source.center;c.size=source.size;
            }
            // Copy the adjoining terrace and rail colliders as well, so endpoint checks cover the actual seam.
            foreach(var source in GameObject.Find("Willow Court").GetComponentsInChildren<BoxCollider>().Where(c=>!c.transform.IsChildOf(garden.transform)&&c.bounds.max.z>=20.3f&&c.bounds.min.z<21.1f)) {
                var g=new GameObject("Test existing boundary");SceneManager.MoveGameObjectToScene(g,scene);
                g.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);g.transform.localScale=source.transform.lossyScale;
                var c=g.AddComponent<BoxCollider>();c.center=source.center;c.size=source.size;
            }
            var body=new GameObject("Test player capsule");SceneManager.MoveGameObjectToScene(body,scene);
            var capsule=body.AddComponent<CapsuleCollider>();capsule.radius=.28f;capsule.height=1.6f;
            var rb=body.AddComponent<Rigidbody>();rb.constraints=RigidbodyConstraints.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            foreach(var source in fence)foreach(float t in new[]{-.5f,0,.5f}) {
                var point=source.transform.TransformPoint(new Vector3(0,0,t*(source.size.z-.18f)));point.y=2.74f;
                var inward=new Vector3(-1.62f-point.x,0,21.055f-point.z).normalized;
                rb.position=point+inward*.8f+Vector3.up*.82f;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
                for(int frame=0;frame<90;frame++) {var velocity=-inward*4;velocity.y=rb.linearVelocity.y;rb.linearVelocity=velocity;physics.Simulate(.02f);}
                runs++;
                if(rb.position.y<2.4f||Vector3.Dot(rb.position-point,-inward)>.35f)escapes++;
            }
        } finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        string result=$"Isolated Rigidbody fence trials: {runs}; escapes: {escapes}.";
        if(escapes!=0)throw new InvalidOperationException(result);
        return result;
    }
}
