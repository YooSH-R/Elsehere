package com.elsehere.common.config;

import com.elsehere.auth.handler.SocialLoginFailureHandler;
import com.elsehere.auth.handler.SocialLoginSuccessHandler;
import com.elsehere.auth.service.SocialLoginService;
import com.elsehere.common.auth.LoginSessionService;
import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;
import org.springframework.boot.webmvc.test.autoconfigure.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.core.annotation.AliasFor;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * 실제 보안 설정({@link SecurityConfig})으로 컨트롤러를 검증하는 웹 슬라이스 테스트.
 *
 * <p>보안 설정이 쓰는 로그인 처리 빈을 함께 등록한다. 웹 슬라이스에는 DB가 없으므로 소셜 로그인 사용자 조회만 Mock으로 둔다.
 */
@Target(ElementType.TYPE)
@Retention(RetentionPolicy.RUNTIME)
@Documented
@WebMvcTest
@Import({
  SecurityConfig.class,
  LoginSessionService.class,
  SocialLoginSuccessHandler.class,
  SocialLoginFailureHandler.class
})
@MockitoBean(types = SocialLoginService.class)
public @interface SecuredWebMvcTest {

  @AliasFor(annotation = WebMvcTest.class, attribute = "controllers")
  Class<?>[] controllers() default {};
}
