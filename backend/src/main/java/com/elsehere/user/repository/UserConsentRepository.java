package com.elsehere.user.repository;

import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.UserConsent;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserConsentRepository extends JpaRepository<UserConsent, Long> {

  /** 철회하지 않은 동의. 버전은 가리지 않는다. */
  List<UserConsent> findByUserIdAndConsentTypeAndWithdrawnAtIsNull(
      Long userId, ConsentType consentType);
}
