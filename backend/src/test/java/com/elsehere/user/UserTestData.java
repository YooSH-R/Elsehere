package com.elsehere.user;

import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;

/** 테스트용 사용자 데이터. 가입 기능(AUTH-04) 전이라 SQL로 넣고, 생성·수정 시각은 DB 기본값으로 채운다. */
public final class UserTestData {

  private UserTestData() {}

  public static Long insertUser(
      EntityManager entityManager, AuthProvider provider, String providerUid) {
    entityManager
        .createNativeQuery(
            "insert into users (provider, provider_uid, nickname, avatar_type, onboarding_step,"
                + " current_language) values (?, ?, 'Sun', 'F', 'DONE', 'en-US')")
        .setParameter(1, provider.name())
        .setParameter(2, providerUid)
        .executeUpdate();
    Number id =
        (Number)
            entityManager
                .createNativeQuery("select id from users where provider_uid = ?")
                .setParameter(1, providerUid)
                .getSingleResult();
    return id.longValue();
  }
}
