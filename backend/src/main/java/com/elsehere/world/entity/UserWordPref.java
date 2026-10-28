package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.vocab.entity.Vocabulary;
import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.MapsId;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/** 단어 카드 자동 표시 설정. 같은 단어가 붙은 모든 사물에 적용한다. 사용자(users)는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다. */
@Entity
@Table(name = "user_word_prefs")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserWordPref extends BaseEntity {

  @EmbeddedId private UserWordPrefId id;

  @MapsId("vocabId")
  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "vocab_id")
  private Vocabulary vocabulary;

  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean hoverHidden;

  /** 사용자가 처음 설정을 바꾼 단어. 설정이 없는 단어는 자동 표시가 켜진 것으로 본다. */
  public static UserWordPref create(Long userId, Vocabulary vocabulary, boolean hoverHidden) {
    UserWordPref pref = new UserWordPref();
    pref.id = new UserWordPrefId(userId, vocabulary.getId());
    pref.vocabulary = vocabulary;
    pref.hoverHidden = hoverHidden;
    return pref;
  }

  public void changeHoverHidden(boolean hoverHidden) {
    this.hoverHidden = hoverHidden;
  }
}
