namespace DgVillage
{
    /// <summary>
    /// 캐릭터가 어느 방향을 보고 있고 걷는 중인지. DirectionalSpriteAnimator 가 이 값만 보고 스프라이트를 고른다.
    /// 내 캐릭터(PlayerMovement, 키보드 입력)와 다른 사람 캐릭터(RemotePlayer, 서버 데이터)가 각각 구현한다.
    /// </summary>
    public interface ICharacterMotion
    {
        Facing Facing { get; }
        bool IsMoving { get; }
    }
}
