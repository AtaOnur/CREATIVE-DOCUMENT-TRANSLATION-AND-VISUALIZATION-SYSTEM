import { z } from "zod";
import {
  documentStatuses,
  operationTypes,
  styleTypes,
} from "@/lib/types/enums";

export const regionRectangleSchema = z.object({
  xNorm: z.number().min(0).max(1),
  yNorm: z.number().min(0).max(1),
  widthNorm: z.number().min(0).max(1),
  heightNorm: z.number().min(0).max(1),
});

export const documentCreateFormSchema = z.object({
  title: z.string().min(1, "Title is required").max(200),
  status: z.enum(documentStatuses).optional(),
});

export const aiRequestFormSchema = z.object({
  operationType: z.enum(operationTypes),
  styleType: z.enum(styleTypes).optional(),
  sourceText: z.string().min(1, "Source text is required").max(50_000),
});

export type RegionRectangleInput = z.infer<typeof regionRectangleSchema>;
export type DocumentCreateFormInput = z.infer<typeof documentCreateFormSchema>;
export type AiRequestFormInput = z.infer<typeof aiRequestFormSchema>;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * React Hook Form ile kullanılmak üzere temel Zod şemalarını tanımlar.
 *
 * [TR] Neden gerekli
 * Sunucu ve istemci doğrulamasını aynı kurallardan türetmek savunmada güven verir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Gelecekteki formlar: belge oluşturma, bölge seçimi payload’u, AI isteği.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: dosya yükleme (multipart), sayfa numarası, kullanıcı rollerine göre alan kısıtı.
 * - Nasıl çözülür: Şemaya alan eklenir; Server Action içinde `safeParse` kullanılır.
 * - Etkilenen dosyalar: Bu dosya, ilgili form bileşenleri, API route’ları.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
