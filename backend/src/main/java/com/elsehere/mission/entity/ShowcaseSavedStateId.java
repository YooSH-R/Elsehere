package com.elsehere.mission.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import java.io.Serializable;
import lombok.AccessLevel;
import lombok.EqualsAndHashCode;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Embeddable
@Getter
@EqualsAndHashCode
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ShowcaseSavedStateId implements Serializable {

  private Long userId;

  // ORIGINAL: 시연 시작 전 계정 상태 · SHOWCASE: 멈춘 시연의 상태
  @Column(length = 10)
  private String kind;
}
