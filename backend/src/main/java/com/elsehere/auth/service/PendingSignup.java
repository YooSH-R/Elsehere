package com.elsehere.auth.service;

import com.elsehere.user.entity.AuthProvider;
import java.io.Serializable;

public record PendingSignup(AuthProvider provider, String providerUid, String email)
    implements Serializable {

  public static final String SESSION_ATTRIBUTE = "PENDING_SIGNUP";
}
