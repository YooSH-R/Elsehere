package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.WordCardResponse;
import com.elsehere.world.dto.WordPrefResponse;
import com.elsehere.world.repository.UserWordPrefRepository;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 단어는 시드(WorldSeedLoader가 시작 때 넣음)를 쓰고, 저장 결과는 WORLD-02 단어 카드로 확인한다.
// 설정 행을 JPA로 저장하므로 생성·수정 시각을 채우는 JPA Auditing 설정을 함께 불러온다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  WordPrefService.class,
  SceneService.class
})
class WordPrefServiceTest {

  @Autowired private WordPrefService wordPrefService;

  @Autowired private SceneService sceneService;

  @Autowired private UserWordPrefRepository userWordPrefRepository;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("설정이 없는 단어의 자동 표시를 끄면 저장되고, 장소 상세에서 hoverHidden이 true로 보인다")
  void hideWord_withoutPref_savesHidden() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    WordPrefResponse response = wordPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(response).isEqualTo(new WordPrefResponse("sofa", true));
    assertThat(hoverHidden("HOME", userId, "sofa")).isTrue();
  }

  @Test
  @DisplayName("단어의 자동 표시를 끄면 그 단어를 쓰는 모든 장소의 카드에서 숨김으로 보인다")
  void hideWord_hidesInEveryScene() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    wordPrefService.changeHoverHidden(userId, "chair", true);

    assertThat(hoverHidden("HOME", userId, "chair")).isTrue();
    assertThat(hoverHidden("CAFE", userId, "chair")).isTrue();
    assertThat(userWordPrefRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("숨긴 단어의 자동 표시를 다시 켜면 같은 설정이 false로 바뀌고 설정은 하나만 남는다")
  void showWord_afterHidden_updatesSamePref() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    wordPrefService.changeHoverHidden(userId, "sofa", true);

    WordPrefResponse response = wordPrefService.changeHoverHidden(userId, "sofa", false);

    assertThat(response).isEqualTo(new WordPrefResponse("sofa", false));
    assertThat(hoverHidden("HOME", userId, "sofa")).isFalse();
    assertThat(userWordPrefRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("이미 숨긴 단어를 다시 숨겨도 설정이 늘지 않고 숨김 상태가 유지된다")
  void hideWord_twice_keepsSinglePref() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    wordPrefService.changeHoverHidden(userId, "sofa", true);

    wordPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(hoverHidden("HOME", userId, "sofa")).isTrue();
    assertThat(userWordPrefRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("없는 단어 키로 설정을 바꾸면 NOT_FOUND 오류를 내고 아무것도 저장하지 않는다")
  void changeHoverHidden_unknownWord_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> wordPrefService.changeHoverHidden(userId, "moon_rock", true))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(userWordPrefRepository.count()).isZero();
  }

  @Test
  @DisplayName("한 사용자가 단어를 숨겨도 다른 사용자의 같은 단어 설정은 바뀌지 않는다")
  void hideWord_doesNotAffectOtherUsers() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    wordPrefService.changeHoverHidden(otherUserId, "sofa", false);

    wordPrefService.changeHoverHidden(userId, "sofa", true);

    assertThat(hoverHidden("HOME", userId, "sofa")).isTrue();
    assertThat(hoverHidden("HOME", otherUserId, "sofa")).isFalse();
    assertThat(userWordPrefRepository.count()).isEqualTo(2);
  }

  private boolean hoverHidden(String sceneCode, Long userId, String wordKey) {
    return sceneService.getScene(sceneCode, userId).words().stream()
        .filter(card -> card.wordKey().equals(wordKey))
        .findFirst()
        .map(WordCardResponse::hoverHidden)
        .orElseThrow();
  }
}
