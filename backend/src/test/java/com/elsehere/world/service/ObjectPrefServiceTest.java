package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.ObjectCardResponse;
import com.elsehere.world.dto.ObjectPrefResponse;
import com.elsehere.world.repository.UserObjectPrefRepository;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 사물은 시드(WorldSeedLoader가 시작 때 넣음)를 쓰고, 저장 결과는 WORLD-02 사물 카드로 확인한다.
// 설정 행을 JPA로 저장하므로 생성·수정 시각을 채우는 JPA Auditing 설정을 함께 불러온다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  ObjectPrefService.class,
  SceneService.class
})
class ObjectPrefServiceTest {

  @Autowired private ObjectPrefService objectPrefService;

  @Autowired private SceneService sceneService;

  @Autowired private UserObjectPrefRepository userObjectPrefRepository;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("설정이 없는 사물의 자동 표시를 끄면 저장되고, 장소 상세에서 hoverHidden이 true로 보인다")
  void hideObject_withoutPref_savesHidden() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    ObjectPrefResponse response = objectPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(response).isEqualTo(new ObjectPrefResponse("sofa", true));
    assertThat(hoverHidden(userId, "sofa")).isTrue();
  }

  @Test
  @DisplayName("숨긴 사물의 자동 표시를 다시 켜면 같은 설정이 false로 바뀌고 설정은 하나만 남는다")
  void showObject_afterHidden_updatesSamePref() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    objectPrefService.changeHoverHidden(userId, "sofa", true);

    ObjectPrefResponse response = objectPrefService.changeHoverHidden(userId, "sofa", false);

    assertThat(response).isEqualTo(new ObjectPrefResponse("sofa", false));
    assertThat(hoverHidden(userId, "sofa")).isFalse();
    assertThat(userObjectPrefRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("이미 숨긴 사물을 다시 숨겨도 설정이 늘지 않고 숨김 상태가 유지된다")
  void hideObject_twice_keepsSinglePref() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    objectPrefService.changeHoverHidden(userId, "sofa", true);

    objectPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(hoverHidden(userId, "sofa")).isTrue();
    assertThat(userObjectPrefRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("없는 사물 키로 설정을 바꾸면 NOT_FOUND 오류를 내고 아무것도 저장하지 않는다")
  void changeHoverHidden_unknownObject_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> objectPrefService.changeHoverHidden(userId, "moon_rock", true))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(userObjectPrefRepository.count()).isZero();
  }

  @Test
  @DisplayName("한 사용자가 사물을 숨겨도 다른 사용자의 같은 사물 설정은 바뀌지 않는다")
  void hideObject_doesNotAffectOtherUsers() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    objectPrefService.changeHoverHidden(otherUserId, "sofa", false);

    objectPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(hoverHidden(userId, "sofa")).isTrue();
    assertThat(hoverHidden(otherUserId, "sofa")).isFalse();
    assertThat(userObjectPrefRepository.count()).isEqualTo(2);
  }

  private boolean hoverHidden(Long userId, String objectKey) {
    return sceneService.getScene("HOME", userId).objects().stream()
        .filter(card -> card.objectKey().equals(objectKey))
        .findFirst()
        .map(ObjectCardResponse::hoverHidden)
        .orElseThrow();
  }
}
