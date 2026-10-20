using System;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 캐릭터 한 종류의 스프라이트 묶음. 캐릭터마다 에셋 하나 (Assets/_Project/Characters/{id}.asset).
    /// 네트워크로는 Id 만 오가고, 각 클라이언트가 CharacterRegistry 에서 Id 로 찾아 쓴다.
    /// 에셋은 Project 창에서 캐릭터 스프라이트 폴더 우클릭 → DgVillage → Create Character Definition 으로 만든다.
    /// </summary>
    public class CharacterDefinition : ScriptableObject
    {
        [Serializable]
        public struct DirectionSprites
        {
            public Sprite Idle;
            public Sprite[] Walk;
        }

        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private DirectionSprites _down;
        [SerializeField] private DirectionSprites _up;
        [SerializeField] private DirectionSprites _right;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public Sprite Preview => _down.Idle;

        /// <summary>Left 는 Right 를 좌우 반전해서 쓰므로 Right 세트를 돌려준다.</summary>
        public DirectionSprites Get(Facing facing) => facing switch
        {
            Facing.Up => _up,
            Facing.Left or Facing.Right => _right,
            _ => _down,
        };
    }
}
