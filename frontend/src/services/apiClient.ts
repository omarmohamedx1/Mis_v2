import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';
import { env } from '../config/env';
import type { ApiErrorResponse } from '../types/api';
import { clearStoredAuth, getStoredAuth } from '../utils/storage';

export const apiClient = axios.create({
  baseURL: env.apiUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use((config) => {
  const auth = getStoredAuth();
  const language = typeof window !== 'undefined' && window.localStorage.getItem('mis.language') === 'ar' ? 'ar' : 'en';

  config.headers.set('Accept-Language', language);

  const requestUrl = `${config.baseURL ?? ''}${config.url ?? ''}`;
  const isLoginRequest = requestUrl.includes('/auth/login');
  if (isLoginRequest) {
    config.headers.delete('Authorization');
  } else if (auth?.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`;
  }

  if (typeof FormData !== 'undefined' && config.data instanceof FormData) {
    config.headers.delete('Content-Type');
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiErrorResponse>) => {
    const requestUrl = `${error.config?.baseURL ?? ''}${error.config?.url ?? ''}`;
    const isLoginRequest = requestUrl.includes('/auth/login');
    const path = typeof window !== 'undefined' ? window.location.pathname : '';

    if (error.response?.status === 401 && getStoredAuth() && !isLoginRequest) {
      clearStoredAuth();

      if (path && path !== '/login') {
        window.location.assign('/login');
      }
    }

    if (error.response?.status === 403 && path && path !== '/unauthorized' && path !== '/login' && path !== '/change-password') {
      window.location.assign('/unauthorized');
    }

    return Promise.reject(error);
  },
);

export type FormDataHttpMethod = 'post' | 'put' | 'patch';

export interface FormDataRequestConfig extends Omit<AxiosRequestConfig<FormData>, 'data' | 'method' | 'url'> {
  method?: FormDataHttpMethod;
}

export async function requestFormData<TResponse>(url: string, formData: FormData, { method = 'post', ...config }: FormDataRequestConfig = {}): Promise<TResponse> {
  const response = await apiClient.request<TResponse>({
    ...config,
    data: formData,
    method,
    url,
  });
  return response.data;
}

export interface ApiFile {
  blob: Blob;
  contentType: string | null;
  fileName: string | null;
}

export type ApiFileRequestConfig = Omit<AxiosRequestConfig, 'responseType' | 'url'>;

function sanitizeFileName(value: string): string | null {
  const fileName = value.split(/[\\/]/).pop()?.trim().replace(/[\u0000-\u001f<>:"|?*]/g, '_');
  return fileName || null;
}

export function getFileNameFromContentDisposition(contentDisposition?: string): string | null {
  if (!contentDisposition) return null;

  const encodedMatch = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(contentDisposition);
  if (encodedMatch?.[1]) {
    const encodedName = encodedMatch[1].trim().replace(/^"|"$/g, '');
    try {
      return sanitizeFileName(decodeURIComponent(encodedName));
    } catch {
      return sanitizeFileName(encodedName);
    }
  }

  const plainMatch = /filename\s*=\s*"?([^";]+)"?/i.exec(contentDisposition);
  return plainMatch?.[1] ? sanitizeFileName(plainMatch[1]) : null;
}

export async function requestApiFile(url: string, config: ApiFileRequestConfig = {}): Promise<ApiFile> {
  const response = await apiClient.request<Blob>({
    ...config,
    method: config.method ?? 'get',
    responseType: 'blob',
    url,
  });
  const contentDisposition = response.headers['content-disposition'];
  const responseContentType = response.headers['content-type'];

  return {
    blob: response.data,
    contentType: typeof responseContentType === 'string' ? responseContentType : response.data.type || null,
    fileName: getFileNameFromContentDisposition(typeof contentDisposition === 'string' ? contentDisposition : undefined),
  };
}

export function saveApiFile(file: ApiFile, fallbackFileName: string): void {
  const objectUrl = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = file.fileName ?? fallbackFileName;
  link.style.display = 'none';
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
}

export async function downloadApiFile(url: string, fallbackFileName: string, config: ApiFileRequestConfig = {}): Promise<ApiFile> {
  const file = await requestApiFile(url, config);
  saveApiFile(file, fallbackFileName);
  return file;
}

export function getApiErrorStatus(error: unknown): number | null {
  return axios.isAxiosError(error) ? error.response?.status ?? null : null;
}

export function isForbiddenApiError(error: unknown): boolean {
  return getApiErrorStatus(error) === 403;
}

export function getApiErrorMessage(error: unknown, fallbackMessage: string): string {
  if (axios.isAxiosError<ApiErrorResponse>(error)) {
    const axiosError = error as AxiosError<ApiErrorResponse>;
    const data = axiosError.response?.data;
    const isArabic = typeof window !== 'undefined' && window.localStorage.getItem('mis.language') === 'ar';
    const details = (data?.errors ?? []).map((item) => item?.trim()).filter(Boolean);
    const serverMessage = data?.message?.trim();
    const generic = serverMessage === 'Validation failed.' || serverMessage === 'تعذر التحقق من صحة البيانات.';
    const parts = generic && details.length ? details : [...new Set([serverMessage, ...details].filter(Boolean))];
    const combined = parts.join(' ');
    if (combined && (!isArabic || /[\u0600-\u06ff]/.test(combined))) return combined;
  }

  return fallbackMessage;
}
