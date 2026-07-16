using System.Collections.Generic;
using UnityEngine;

/// 挂在腿部骨骼（或任何障碍物）上的"鱼避让区"（球形）。
/// 场景里所有启用中的避让区会被鱼自动绕开。
/// 在 Scene 视图里选中挂了这个组件的物体，可以看到橙色线框球即避让范围。
public class FishAvoidZone : MonoBehaviour
{
    [Tooltip("避让球体的半径（米，世界空间）")]
    public float radius = 0.045f;

    public static readonly List<FishAvoidZone> Active = new List<FishAvoidZone>();

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
