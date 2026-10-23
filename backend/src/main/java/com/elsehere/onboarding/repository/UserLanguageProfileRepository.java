package com.elsehere.onboarding.repository;

import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserLanguageProfileRepository
    extends JpaRepository<UserLanguageProfile, UserLanguageProfileId> {}
