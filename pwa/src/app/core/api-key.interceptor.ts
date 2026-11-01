import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../environments/environment';

/**
 * Anexa o header X-Api-Key nas chamadas pra API, quando uma chave estiver configurada.
 * Se `environment.apiKey` estiver vazio (padrão em desenvolvimento local), a requisição
 * segue sem alteração, exatamente como antes desse interceptor existir.
 *
 * Tambem manda ngrok-skip-browser-warning em toda chamada pra /api: sem isso, o plano
 * gratuito do ngrok intercepta a chamada (cookie de "ja visitei" expira ou nao existe
 * ainda nessa sessao) e devolve a pagina de aviso em HTML em vez do JSON da API - o
 * Angular falha ao decodificar e a PWA mostra "nao foi possivel falar com a API"
 * mesmo com tudo no ar. So importa quando testando via tunel; fora do ngrok e inofensivo.
 */
export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const headers: Record<string, string> = { 'ngrok-skip-browser-warning': 'true' };
  if (environment.apiKey) {
    headers['X-Api-Key'] = environment.apiKey;
  }

  return next(req.clone({ setHeaders: headers }));
};
