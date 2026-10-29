package com.elsehere.user.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.UserTestData;
import com.elsehere.user.dto.NicknameChangeResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, NicknameService.class})
class NicknameServiceTest {

  @Autowired private NicknameService nicknameService;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("아무도 쓰지 않는 닉네임은 쓸 수 있다")
  void unusedNickname_isAvailable() {
    assertThat(nicknameService.isAvailable("Sun")).isTrue();
  }

  @Test
  @DisplayName("다른 사용자가 쓰는 닉네임은 쓸 수 없다")
  void takenNickname_isNotAvailable() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThat(nicknameService.isAvailable("Sun")).isFalse();
  }

  @Test
  @DisplayName("닉네임을 바꾸면 바꾼 닉네임을 돌려주고 계정에 저장한다")
  void changeNickname_savesNewNickname() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    NicknameChangeResponse response = nicknameService.changeNickname(userId, "선종");

    assertThat(response).isEqualTo(new NicknameChangeResponse("선종"));
    assertThat(savedNickname(userId)).isEqualTo("선종");
  }

  @Test
  @DisplayName("다른 사용자가 쓰는 닉네임으로는 바꾸지 않고 NICKNAME_TAKEN으로 알린다")
  void changeNickname_toTakenNickname_isRejected() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long userId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    assertThatThrownBy(() -> nicknameService.changeNickname(userId, "Sun"))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.NICKNAME_TAKEN);
    assertThat(savedNickname(userId)).isEqualTo("Moon");
  }

  @Test
  @DisplayName("지금 쓰는 닉네임을 그대로 보내면 중복으로 보지 않고 그대로 둔다")
  void changeNickname_toCurrentNickname_keepsIt() {
    Long userId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1", "Sun");

    NicknameChangeResponse response = nicknameService.changeNickname(userId, "Sun");

    assertThat(response).isEqualTo(new NicknameChangeResponse("Sun"));
    assertThat(savedNickname(userId)).isEqualTo("Sun");
  }

  // 저장한 값이 DB에 반영됐는지 보려고 영속성 컨텍스트를 비운 뒤 다시 읽는다.
  private String savedNickname(Long userId) {
    entityManager.flush();
    entityManager.clear();
    return entityManager.find(User.class, userId).getNickname();
  }
}
