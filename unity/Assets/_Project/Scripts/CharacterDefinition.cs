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

        [Tooltip("대각선 (선택). 비어 있으면 가까운 위/아래 방향으로 대신한다")]
        [SerializeField] private DirectionSprites _downRight;
        [SerializeField] private DirectionSprites _upRight;

        [Tooltip("앉은 모습 (정면). 없으면 서기 그림을 올려서 쓴다")]
        [SerializeField] private Sprite _sitDown;

        [Tooltip("옆 · 뒤를 보는 의자에 앉은 모습 (선택). 왼쪽은 오른쪽을 반전. 없으면 그 방향 서기 그림을 올려서 쓴다")]
        [SerializeField] private Sprite _sitRight;
        [SerializeField] private Sprite _sitUp;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public Sprite Preview => _down.Idle;

        /// <summary>정면 앉은 모습 (캐릭터 선택 등). 없으면 null</summary>
        public Sprite SitDown => _sitDown;

        /// <summary>facing 방향 의자에 앉은 모습. 대각선은 위/아래로 친다. 없으면 null (서기 그림을 올려 쓴다)</summary>
        public Sprite GetSit(Facing facing, out bool flipX)
        {
            flipX = facing == Facing.Left;
            return facing switch
            {
                Facing.Left or Facing.Right => _sitRight,
                Facing.Up or Facing.UpLeft or Facing.UpRight => _sitUp,
                _ => _sitDown,
            };
        }

        /// <summary>Left 는 Right 를 좌우 반전해서 쓰므로 Right 세트를 돌려준다.</summary>
        public DirectionSprites Get(Facing facing) => Get(facing, out _);

        /// <summary>
        /// facing 에 쓸 스프라이트 세트. 왼쪽 계열(Left · DownLeft · UpLeft)은 오른쪽 그림을 반전하므로 flipX = true.
        /// 대각선 그림이 없는 캐릭터는 위/아래 그림으로 대신한다 (8방향 이전과 같은 모습).
        /// </summary>
        public DirectionSprites Get(Facing facing, out bool flipX)
        {
            flipX = false;
            switch (facing)
            {
                case Facing.Up:
                    return _up;
                case Facing.Left:
                case Facing.Right:
                    flipX = facing == Facing.Left;
                    return _right;
                case Facing.UpLeft:
                case Facing.UpRight:
                    if (_upRight.Idle == null)
                    {
                        return _up;
                    }
                    flipX = facing == Facing.UpLeft;
                    return _upRight;
                case Facing.DownLeft:
                case Facing.DownRight:
                    if (_downRight.Idle == null)
                    {
                        return _down;
                    }
                    flipX = facing == Facing.DownLeft;
                    return _downRight;
                default:
                    return _down;
            }
        }
    }
}
