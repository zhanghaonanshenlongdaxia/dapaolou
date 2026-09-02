using UnityEngine;
using System.Collections.Generic;
using Dapaolou.Marble;

namespace Dapaolou.Game
{
    /// <summary>
    /// 炮楼建造器 - 创建炮楼结构
    /// </summary>
    public class TowerBuilder : MonoBehaviour
    {
        [Header("炮楼配置")]
        [SerializeField] private GameObject marblePrefab;           // 弹珠预制体
        [SerializeField] private int baseMarbleCount = 3;          // 底部弹珠数
        [SerializeField] private float marbleRadius = 0.025f;      // 弹珠半径
        [SerializeField] private float spacingFactor = 1.05f;      // 间距系数
        
        [Header("外观配置")]
        [SerializeField] private Material towerMaterial;           // 炮楼材质
        [SerializeField] private Material highlightMaterial;       // 高亮材质
        [SerializeField] private Color destroyColor = Color.red;   // 被摧毁颜色
        
        [Header("物理配置")]
        [SerializeField] private float towerMass = 0.5f;           // 炮楼总质量
        [SerializeField] private float stabilityForce = 10f;       // 稳定性力
        
        /// <summary>
        /// 创建炮楼
        /// </summary>
        /// <param name="center">炮楼中心位置</param>
        /// <param name="playerId">所属玩家ID</param>
        /// <param name="playerColor">玩家颜色</param>
        /// <returns>创建的炮楼弹珠列表</returns>
        public List<MarbleData> BuildTower(Vector3 center, int playerId, Color playerColor)
        {
            List<MarbleData> towerMarbles = new List<MarbleData>();
            
            // 计算底部弹珠位置（三角形排列）
            List<Vector3> basePositions = CalculateBasePositions(center);
            
            // 创建底部弹珠
            for (int i = 0; i < basePositions.Count; i++)
            {
                GameObject marbleObj = CreateMarble(basePositions[i], MarbleType.Tower, playerId, i);
                towerMarbles.Add(marbleObj.GetComponent<MarbleData>());
            }
            
            // 创建顶部弹珠（放在底部中心上方）
            Vector3 topPosition = CalculateTopPosition(basePositions);
            GameObject topMarbleObj = CreateMarble(topPosition, MarbleType.Tower, playerId, baseMarbleCount);
            towerMarbles.Add(topMarbleObj.GetComponent<MarbleData>());
            
            // 设置颜色
            SetTowerColor(towerMarbles, playerColor);
            
            // 添加稳定性约束
            AddStabilityConstraints(towerMarbles);
            
            Debug.Log($"Tower built for player {playerId} at {center} with {towerMarbles.Count} marbles");
            return towerMarbles;
        }
        
        /// <summary>
        /// 计算底部弹珠位置（三角形排列）
        /// </summary>
        private List<Vector3> CalculateBasePositions(Vector3 center)
        {
            List<Vector3> positions = new List<Vector3>();
            float spacing = marbleRadius * 2 * spacingFactor;
            
            if (baseMarbleCount == 3)
            {
                // 三角形排列
                positions.Add(center + new Vector3(0, marbleRadius, 0));
                positions.Add(center + new Vector3(-spacing * 0.5f, marbleRadius, spacing * 0.866f));
                positions.Add(center + new Vector3(spacing * 0.5f, marbleRadius, spacing * 0.866f));
            }
            else if (baseMarbleCount == 4)
            {
                // 正方形排列
                float halfSpacing = spacing * 0.5f;
                positions.Add(center + new Vector3(-halfSpacing, marbleRadius, -halfSpacing));
                positions.Add(center + new Vector3(halfSpacing, marbleRadius, -halfSpacing));
                positions.Add(center + new Vector3(-halfSpacing, marbleRadius, halfSpacing));
                positions.Add(center + new Vector3(halfSpacing, marbleRadius, halfSpacing));
            }
            else
            {
                // 圆形排列
                for (int i = 0; i < baseMarbleCount; i++)
                {
                    float angle = (360f / baseMarbleCount) * i * Mathf.Deg2Rad;
                    float x = Mathf.Cos(angle) * spacing;
                    float z = Mathf.Sin(angle) * spacing;
                    positions.Add(center + new Vector3(x, marbleRadius, z));
                }
            }
            
            return positions;
        }
        
        /// <summary>
        /// 计算顶部弹珠位置
        /// </summary>
        private Vector3 CalculateTopPosition(List<Vector3> basePositions)
        {
            // 计算底部中心
            Vector3 center = Vector3.zero;
            foreach (var pos in basePositions)
            {
                center += pos;
            }
            center /= basePositions.Count;
            
            // 顶部在中心上方
            return center + Vector3.up * marbleRadius * 2 * spacingFactor;
        }
        
        /// <summary>
        /// 创建单个弹珠
        /// </summary>
        private GameObject CreateMarble(Vector3 position, MarbleType type, int playerId, int index)
        {
            GameObject marbleObj;
            
            if (marblePrefab != null)
            {
                marbleObj = Instantiate(marblePrefab, position, Quaternion.identity);
            }
            else
            {
                // 创建默认弹珠
                marbleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marbleObj.transform.position = position;
                marbleObj.transform.localScale = Vector3.one * marbleRadius * 2;
                
                // 添加物理组件
                Rigidbody rb = marbleObj.AddComponent<Rigidbody>();
                rb.mass = towerMass / (baseMarbleCount + 1);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                
                // 添加物理材质
                SphereCollider collider = marbleObj.GetComponent<SphereCollider>();
                if (collider != null)
                {
                    PhysicMaterial physMat = new PhysicMaterial("Marble");
                    physMat.dynamicFriction = 0.4f;
                    physMat.staticFriction = 0.4f;
                    physMat.bounciness = 0.3f;
                    collider.material = physMat;
                }
            }
            
            // 添加弹珠数据组件
            MarbleData marbleData = marbleObj.GetComponent<MarbleData>();
            if (marbleData == null)
            {
                marbleData = marbleObj.AddComponent<MarbleData>();
            }
            
            marbleData.marbleType = type;
            marbleData.ownerPlayerId = playerId;
            marbleData.towerIndex = (type == MarbleType.Tower) ? index : -1;
            
            // 设置名称
            marbleObj.name = $"Player{playerId}_{type}_{index}";
            
            return marbleObj;
        }
        
        /// <summary>
        /// 设置炮楼颜色
        /// </summary>
        private void SetTowerColor(List<MarbleData> marbles, Color color)
        {
            foreach (var marble in marbles)
            {
                Renderer renderer = marble.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = color;
                }
            }
        }
        
        /// <summary>
        /// 添加稳定性约束（防止炮楼倒塌）
        /// </summary>
        private void AddStabilityConstraints(List<MarbleData> marbles)
        {
            // 添加FixedJoint保持炮楼结构
            for (int i = 0; i < marbles.Count - 1; i++)
            {
                FixedJoint joint = marbles[i].gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = marbles[i + 1].GetComponent<Rigidbody>();
                joint.breakForce = stabilityForce;
                joint.breakTorque = stabilityForce;
            }
        }
        
        /// <summary>
        /// 摧毁炮楼（播放破碎效果）
        /// </summary>
        public void DestroyTower(List<MarbleData> marbles, Vector3 impactPoint, float force)
        {
            foreach (var marble in marbles)
            {
                if (marble == null) continue;
                
                // 移除FixedJoint
                FixedJoint joint = marble.GetComponent<FixedJoint>();
                if (joint != null)
                {
                    Destroy(joint);
                }
                
                // 添加爆炸力
                Rigidbody rb = marble.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.AddExplosionForce(force, impactPoint, 0.5f, 0.5f, ForceMode.Impulse);
                }
                
                // 改变颜色
                Renderer renderer = marble.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = destroyColor;
                }
                
                // 标记为销毁
                marble.OnDestroyed();
            }
        }
        
        /// <summary>
        /// 检查炮楼是否完整
        /// </summary>
        public bool IsTowerIntact(List<MarbleData> marbles)
        {
            if (marbles.Count < 2) return false; // 至少需要底+顶
            
            // 检查所有弹珠是否存活
            foreach (var marble in marbles)
            {
                if (marble == null || marble.state == MarbleState.Destroyed)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 检查炮楼是否需要二次打爆
        /// </summary>
        public bool NeedsSecondHit(List<MarbleData> marbles)
        {
            int aliveCount = 0;
            foreach (var marble in marbles)
            {
                if (marble != null && marble.state != MarbleState.Destroyed)
                {
                    aliveCount++;
                }
            }
            
            // 剩余1-3个弹珠且相邻，需要二次打爆
            return aliveCount > 0 && aliveCount <= 3;
        }
        
        /// <summary>
        /// 获取炮楼中心位置
        /// </summary>
        public Vector3 GetTowerCenter(List<MarbleData> marbles)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            
            foreach (var marble in marbles)
            {
                if (marble != null)
                {
                    center += marble.transform.position;
                    count++;
                }
            }
            
            return count > 0 ? center / count : Vector3.zero;
        }
        
        void OnDrawGizmosSelected()
        {
            // 绘制炮楼结构预览
            Gizmos.color = Color.yellow;
            
            // 底部三角形
            float spacing = marbleRadius * 2 * spacingFactor;
            Vector3 center = transform.position;
            
            Gizmos.DrawWireSphere(center + new Vector3(0, marbleRadius, 0), marbleRadius);
            Gizmos.DrawWireSphere(center + new Vector3(-spacing * 0.5f, marbleRadius, spacing * 0.866f), marbleRadius);
            Gizmos.DrawWireSphere(center + new Vector3(spacing * 0.5f, marbleRadius, spacing * 0.866f), marbleRadius);
            
            // 顶部
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center + Vector3.up * marbleRadius * 3, marbleRadius);
        }
    }
}
