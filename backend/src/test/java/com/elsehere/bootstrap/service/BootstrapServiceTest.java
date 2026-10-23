package com.elsehere.bootstrap.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.bootstrap.dto.AuthState;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
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
@Import({TestcontainersConfiguration.class, BootstrapService.class})
class BootstrapServiceTest {

  @Autowired private EntityManager entityManager;

  @Autowired private BootstrapService bootstrapService;

  @Test
  @DisplayName("가입한 사용자면 로그인 상태와 사용자 정보, 초기 설정 단계를 돌려준다")
  void signedInUser_returnsUserAndOnboardingStep() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.authState()).isEqualTo(AuthState.SIGNED_IN);
    assertThat(response.user().userId()).isEqualTo(userId);
    assertThat(response.user().nickname()).isEqualTo("Sun");
    assertThat(response.user().avatarType()).isEqualTo(AvatarType.F);
    assertThat(response.user().currentLanguage()).isEqualTo("en-US");
    assertThat(response.user().onboardingStep()).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("마지막으로 머문 장소가 있으면 재접속할 장소 코드를 돌려준다")
  void userWithLastScene_returnsLastSceneCode() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setLastScene(userId, "CAFE");

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.lastSceneCode()).isEqualTo("CAFE");
  }

  @Test
  @DisplayName("세션의 사용자가 DB에 없으면(탈퇴, 없는 ID로 개발용 로그인) 빈 값을 돌려준다")
  void missingUser_returnsEmpty() {
    assertThat(bootstrapService.findSignedIn(999L)).isEmpty();
  }

  // 장소 이동 저장(WORLD-05) 전이라 SQL로 장소를 만들고 마지막 장소로 지정한다.
  private void setLastScene(Long userId, String sceneCode) {
    entityManager
        .createNativeQuery("insert into scenes (code, name, unity_key) values (?, ?, ?)")
        .setParameter(1, sceneCode)
        .setParameter(2, sceneCode)
        .setParameter(3, sceneCode.toLowerCase())
        .executeUpdate();
    entityManager
        .createNativeQuery(
            "update users set last_scene_id = (select id from scenes where code = ?) where id = ?")
        .setParameter(1, sceneCode)
        .setParameter(2, userId)
        .executeUpdate();
  }
}
