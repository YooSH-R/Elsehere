package com.elsehere.world.repository;

import com.elsehere.world.entity.UserObjectPref;
import com.elsehere.world.entity.UserObjectPrefId;
import java.util.Collection;
import java.util.Set;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface UserObjectPrefRepository extends JpaRepository<UserObjectPref, UserObjectPrefId> {

  /** 주어진 사물 중 사용자가 카드 자동 표시를 끈 사물의 ID. 설정이 없는 사물은 자동 표시가 켜진 것으로 본다. */
  @Query(
      "select pref.id.objectId from UserObjectPref pref"
          + " where pref.id.userId = :userId and pref.hoverHidden = true"
          + " and pref.id.objectId in :objectIds")
  Set<Long> findHoverHiddenObjectIds(
      @Param("userId") Long userId, @Param("objectIds") Collection<Long> objectIds);
}
