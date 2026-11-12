package com.elsehere.admin.dto;

/** 어드민 화면에서 사용자를 부르는 이름. 가입자는 이메일이고, 이메일이 없으면 구분과 사용자 ID로 부른다. */
public final class AdminUserName {

  private AdminUserName() {}

  /**
   * @param withdrawn 탈퇴했는지. 탈퇴하면 이메일을 지우므로(User.withdraw) 구분으로 부른다
   */
  public static String of(long userId, boolean guest, boolean withdrawn, String email) {
    if (withdrawn) {
      return "탈퇴 #" + userId;
    }
    if (guest) {
      return "게스트 #" + userId;
    }
    return email != null ? email : "회원 #" + userId;
  }
}
