package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
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

/** 사물 카드 자동 표시 설정. 사용자(users)는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다. */
@Entity
@Table(name = "user_object_prefs")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserObjectPref extends BaseEntity {

  @EmbeddedId private UserObjectPrefId id;

  @MapsId("objectId")
  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "object_id")
  private WorldObject worldObject;

  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean hoverHidden;

  /** 사용자가 처음 설정을 바꾼 사물. 설정이 없는 사물은 자동 표시가 켜진 것으로 본다. */
  public static UserObjectPref create(Long userId, WorldObject worldObject, boolean hoverHidden) {
    UserObjectPref pref = new UserObjectPref();
    pref.id = new UserObjectPrefId(userId, worldObject.getId());
    pref.worldObject = worldObject;
    pref.hoverHidden = hoverHidden;
    return pref;
  }

  public void changeHoverHidden(boolean hoverHidden) {
    this.hoverHidden = hoverHidden;
  }
}
