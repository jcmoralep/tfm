# Recorrido de `registration-hardening` (2026-10-15)

Observaciones de la validación manual (aplicación con MySQL local, un receptor SMTP local que guarda los correos —un pequeño script, en lugar de Mailpit—, claves de prueba de Cloudflare Turnstile, usuarios temporales). Sin capturas en el repositorio, por decisión del usuario.

Configuración de la primera corrida: `Captcha__Provider=Turnstile` con las claves de prueba que siempre aprueban, `Email__Provider=Smtp`, `Email__RequireConfirmedEmail=true`, `Email__PublicBaseUrl=http://localhost:5080`, SMTP en `localhost:1025` sin TLS.

| Área | Observado |
|---|---|
| Captcha (S2) | `/registro` muestra el widget y se genera un token (21 caracteres, el token ficticio de prueba); un POST con token vacío muestra «Completa la verificación para continuar.» y no crea cuenta. Con la clave secreta de prueba que siempre falla, un POST a `/recuperar-contrasena` muestra el mismo mensaje y no envía correo. Sin configurar nada (`Captcha:Provider=None`): sin widget y sin peticiones a Cloudflare. El nombre exacto del campo (`cf-turnstile-response`) llega al servidor en el POST; no se distinguió si lo enlaza el atributo o la lectura de respaldo del formulario, y las dos rutas funcionan. |
| Registro con confirmación (S4) | Mensaje «Revisa tu correo…», sin sesión iniciada, un correo «Confirma tu correo en Ruta de Producto» con enlace de `http://localhost:5080/confirmar-correo?…`. Un registro duplicado muestra el mismo mensaje y el dueño recibe «Ya tienes una cuenta en Ruta de Producto» (con enlaces a iniciar sesión y a recuperar); no se creó una segunda cuenta. |
| Inicio de sesión sin confirmar | Con la contraseña correcta: «Confirma tu correo antes de iniciar sesión.» con el enlace de reenvío; con una contraseña errónea y con un correo desconocido: el mismo mensaje genérico de credenciales incorrectas. |
| Enlace de confirmación | Un `codigo` alterado muestra «No pudimos confirmar tu correo. El enlace no es válido o venció.» con enlace para pedir otro. El enlace válido confirma y permite iniciar sesión. **Desviación de la spec:** volver a abrir el mismo enlace tras confirmar sigue mostrando «Correo confirmado» (Identity no rota el sello de seguridad al confirmar; el efecto es idempotente: solo confirma un correo ya confirmado). Se registra en la spec como comportamiento aceptado. |
| Reenvío (S4) | Un correo desconocido, uno ya confirmado y uno sin confirmar muestran el mismo texto; solo el sin confirmar recibió correo. Tiempos de respuesta similares (12 a 32 ms). |
| Recuperación (S5) | Un correo desconocido y uno existente muestran el mismo texto («…Vence en 2 horas.»); solo el existente recibió «Restablece tu contraseña de Ruta de Producto». Una contraseña débil («abc») muestra las reglas y el enlace sigue sirviendo; una válida da «Contraseña actualizada», no inicia sesión, la contraseña anterior deja de funcionar; reutilizar el enlace muestra «El enlace no es válido o venció.» |
| Enlaces del correo | Con una cabecera `Host: evil.example:5080` falsificada en la petición, el enlace del correo siguió usando `http://localhost:5080`. |
| Limitador (S1) con correo | Con `RateLimiting__Enabled=true`: un POST a `/recuperar-contrasena` más nueve reenvíos dieron respuestas 200 y los siguientes 429 con `Retry-After`. |
| Valores por defecto | Sin configurar correo ni captcha: el enlace «¿Olvidaste tu contraseña?» y el de reenvío no aparecen, no hay widget, el registro funciona y deja la sesión iniciada. Las tres páginas de recuperación y reenvío respondían 500 (`Navigation.NotFound()` chocaba con el re-ejecutor de códigos de estado); **se corrigió** redirigiendo a `/not-found?codigo=404`, que ahora responde 404 con «Página no encontrada». |
| Registros | Sin correos, tokens, enlaces ni contraseñas en `Logs/`; solo el identificador de usuario y el resultado. Un aviso inofensivo sobre el puerto HTTPS de redirección. |

No se ejercitó: el widget con JavaScript desactivado, el widget con la clave de sitio que siempre bloquea, el bloqueo del dominio de Cloudflare, el efecto de reiniciar la aplicación sobre enlaces ya enviados (las claves de Data Protection no persisten en Railway; está documentado), entregas reales por Gmail/Outlook.

## Cobertura de escenarios

Auditoría de la spec `openspec/changes/registration-hardening/specs/authentication/spec.md` (45 escenarios tras el cierre de la verificación: 37 [UNIT], 6 [UI], 1 [DOC], 1 [MANUAL]). Las pruebas están en `tests/`; «pendiente» significa sin observar todavía.

### Escenarios [UNIT]

| Requisito | Escenario | Prueba |
|---|---|---|
| Limitación | Default is off | `RateLimitOptionsTests > Absent_settings_give_the_disabled_defaults` (la mitad de `Program` se vio en el humo de S1) |
| Limitación | Only auth POSTs are selected | `AuthRateLimitingTests > Only_auth_posts_are_selected` (incluye las variantes con barra final) |
| Limitación | Invalid numbers fail at startup | `RateLimitOptionsTests > Bad_numbers_fail_only_when_enabled_and_never_echo_the_value`, `A_non_boolean_enabled_throws...` |
| Limitación | Partition is per IP | `AuthRateLimitingTests > The_partition_is_the_client_ip_or_unknown`, `The_eleventh_post_of_an_ip_is_refused...` |
| Captcha | Default provider is None | `CaptchaOptionsTests > Without_a_provider_the_captcha_is_off_and_passes_anything` |
| Captcha | Unknown provider lists valid values | `CaptchaOptionsTests > An_unknown_provider_lists_the_valid_values` |
| Captcha | Turnstile requires its keys | `CaptchaOptionsTests > Turnstile_requires_both_keys...`, `A_configured_secret_never_appears...` |
| Captcha | Siteverify outcomes | `TurnstileCaptchaVerifierTests > Siteverify_answers_map_to_an_outcome`, `A_timeout_...`, `A_network_error_...` |
| Captcha | Empty token makes no call | `TurnstileCaptchaVerifierTests > A_missing_blank_or_oversized_token_is_rejected_without_a_call` |
| Captcha | Failure blocks registration before any lookup | `RegisterCommandTests > A_captcha_that_did_not_pass_creates_no_account_signs_no_one_in_and_emails_nobody` |
| Captcha | Messages do not leak | `TurnstileCaptchaVerifierTests > The_request_carries_only_the_secret_and_the_token_and_nothing_leaks_to_the_log` |
| Correo | Default provider is None | `EmailRegistrationTests > Without_configuration_...`, `EmailOptionsTests > Without_a_provider_email_is_off...` |
| Correo | Smtp requires its settings | `EmailOptionsTests > An_invalid_smtp_configuration_stops_the_app...`, `A_local_sink_needs_no_tls...` |
| Correo | Unknown provider | `EmailOptionsTests > An_unknown_provider_lists_the_valid_values` |
| Correo | Queue returns before sending | `EmailDispatcherTests > Enqueue_returns_at_once...`, `The_101st_message_is_dropped...` |
| Correo | Sender failure is contained | `EmailDispatcherTests > A_failing_send_is_logged_without_the_address_and_the_next_message_still_goes` |
| Correo | Links come from configuration | `AccountLinksTests` (4 pruebas) y la comprobación de la cabecera `Host` falsificada en el recorrido |
| Confirmación | Startup guard | `EmailOptionsTests > Requiring_confirmation_without_smtp_stops_the_app`, `..._with_smtp_passes` |
| Confirmación | Default behavior is unchanged | `RegisterCommandTests > With_confirmation_off_...` |
| Confirmación | Registration with confirmation required | `RegisterCommandTests > With_confirmation_on_a_new_account_gets_a_confirmation_email_and_is_not_signed_in` |
| Confirmación | Duplicate address is indistinguishable | `RegisterCommandTests > With_confirmation_on_a_duplicate_address_gets_the_same_result_and_an_account_exists_email` |
| Confirmación | Mail goes to the stored address (nuevo) | `RegisterCommandTests > With_confirmation_on_a_duplicate_matched_by_collation_...` y `..._the_confirmation_goes_to_the_stored_address`; `AccountConfirmationTests > Resend_mails_the_stored_address_...`; `PasswordRecoveryTests > The_reset_mail_goes_to_the_stored_address_...`; `ResetTokenTests > Tokens_and_lookups_carry_the_stored_address_not_the_typed_one`; validadores que rechazan no ASCII en las tres clases de pruebas |
| Confirmación | Confirmation succeeds and fails uniformly | `ConfirmationTokenTests` (válido y seis casos inválidos) |
| Confirmación | NotAllowed is gated by the password | `SignInOutcomeTests > NotAllowed_is_gated_by_the_lockout_and_by_the_password` (parte pura) y `SignInWiringTests` (bloqueo, conteo de fallos con `AccessFailedAsync`, contraseña correcta sin contar) |
| Confirmación | Unknown email pays the hash cost | `SignInOutcomeTests > The_dummy_hash_is_computed_once_...` y `SignInWiringTests > An_unknown_email_pays_one_dummy_hash_verification_...` |
| Confirmación | Resend is uniform | `AccountConfirmationTests > Resend_gives_the_same_result_and_mails_only_a_pending_account` y el recorrido «Reenvío (S4)» |
| Confirmación | Seeded users keep working | `InitialUserSeederTests > Seeded_users_are_created_with_a_confirmed_email...` (comprueba el indicador; el inicio de sesión real no se observó en Chrome) |
| Recuperación | Forgot is uniform | `PasswordRecoveryTests > Forgot_gives_the_same_result_and_mails_only_an_existing_account_a_reset_link` |
| Recuperación | Captcha runs before the lookup | `PasswordRecoveryTests > A_failed_captcha_stops_before_any_lookup` |
| Recuperación | Reset succeeds | `ResetTokenTests > A_valid_token_resets_the_password_once` |
| Recuperación | Reset rejects bad tokens uniformly | `ResetTokenTests > Every_bad_token_gives_the_same_invalid_link_and_never_throws` |
| Recuperación | Reset enforces the password policy | `ResetTokenTests > A_weak_password_is_reported_in_spanish_and_leaves_the_link_usable`; el validador solo comprueba la forma (`PasswordRecoveryTests > The_reset_validator_uses_the_registration_messages`) |
| Recuperación | Reset confirms the email | `ResetTokenTests > Any_existing_account_gets_a_token_and_a_reset_confirms_the_address` |
| Data Protection | Unprotectable token is a uniform failure | filas «another-key-ring» de `ConfirmationTokenTests` y `ResetTokenTests` |
| Secretos | Placeholders only | `AppSettingsSecretsTests` (los dos archivos) |
| Secretos | No content in logs | `ToString()` de comandos y `AccountToken`; `AccountTokenLogTests > The_token_flows_log_no_address_code_or_password` (captura de registros del servicio de tokens); pruebas de registro del despachador y de Turnstile. Los manejadores no tienen `ILogger` |
| Secretos | Defaults change nothing | `EmailRegistrationTests` (sin trabajador) y toda la suite previa sin editar |

### Escenarios [UI], [DOC] y [MANUAL]

| Requisito | Escenario | Evidencia |
|---|---|---|
| Limitación | Eleventh POST is rejected [UI] | Notas de S1 en `tasks.md`: diez 200, el 11.º y 12.º 429 con `Retry-After: 60`, página en español, GET correcto, registro solo con la ruta |
| Captcha | Widget and test keys [UI] | Fila «Captcha (S2)» de este documento. Pendiente: texto en español del widget, `/login` sin widget, JavaScript desactivado, clave que siempre bloquea, dominio de Cloudflare bloqueado, ficha nueva tras un error de validación |
| Correo | Real delivery [UI] | Filas «Registro con confirmación», «Recuperación» y «Registros» de este documento |
| Correo | Provider deliverability [MANUAL] | Pendiente: nunca ejecutado (Gmail/Outlook y la política de SMTP saliente de Railway); anotado en `docs/mejoras-posteriores.md` |
| Confirmación | Full flow [UI] | Filas «Registro con confirmación», «Inicio de sesión sin confirmar» y «Enlace de confirmación» (el enlace reabierto sigue confirmando, nota R24). Pendiente: cinco contraseñas erróneas que bloquean la cuenta (cubierto por `SignInWiringTests`) |
| Recuperación | Link hidden without email [UI] | Fila «Valores por defecto» (enlace oculto y las tres páginas con 302 a `/not-found?codigo=404` y luego 404) |
| Recuperación | Full flow [UI] | Fila «Recuperación (S5)» |
| Data Protection | Caveat documented [DOC] | `README.md` (viñetas «Redespliegue») y `AGENTS.md` §4.6 (viñeta «Redespliegue y Data Protection») |

## Comprobación de extremo a extremo de la corrección C1 (2026-10-15)

Con MySQL real, correo y captcha activos: escribir `NUEVO1@EXAMPLE.COM` en «olvidé mi contraseña» envió el correo a la dirección guardada `nuevo1@example.com`; una variante con tilde (`nuevó1@example.com`) se rechazó con el mensaje neutro «El correo electrónico solo puede llevar letras sin acentos…» sin enviar nada; una dirección desconocida mostró el texto uniforme y no generó correo.
