const governorates: Record<string, [string, string]> = {
  '01': ['القاهرة', 'Cairo'],
  '02': ['الإسكندرية', 'Alexandria'],
  '03': ['بورسعيد', 'Port Said'],
  '04': ['السويس', 'Suez'],
  '11': ['دمياط', 'Damietta'],
  '12': ['الدقهلية', 'Dakahlia'],
  '13': ['الشرقية', 'Sharqia'],
  '14': ['القليوبية', 'Qalyubia'],
  '15': ['كفر الشيخ', 'Kafr El Sheikh'],
  '16': ['الغربية', 'Gharbia'],
  '17': ['المنوفية', 'Monufia'],
  '18': ['البحيرة', 'Beheira'],
  '19': ['الإسماعيلية', 'Ismailia'],
  '21': ['الجيزة', 'Giza'],
  '22': ['بني سويف', 'Beni Suef'],
  '23': ['الفيوم', 'Fayoum'],
  '24': ['المنيا', 'Minya'],
  '25': ['أسيوط', 'Asyut'],
  '26': ['سوهاج', 'Sohag'],
  '27': ['قنا', 'Qena'],
  '28': ['أسوان', 'Aswan'],
  '29': ['الأقصر', 'Luxor'],
  '31': ['البحر الأحمر', 'Red Sea'],
  '32': ['الوادي الجديد', 'New Valley'],
  '33': ['مطروح', 'Matrouh'],
  '34': ['شمال سيناء', 'North Sinai'],
  '35': ['جنوب سيناء', 'South Sinai'],
  '88': ['خارج الجمهورية', 'Outside Egypt'],
};

export type NationalIdCard = {
  birthDate: Date;
  gender: 'male' | 'female';
  governorateCode: string;
  governorateAr: string;
  governorateEn: string;
  age: number;
};

export function readNationalIdCard(value?: string | null, today = new Date()): NationalIdCard | null {
  const digits = (value ?? '').replace(/\D/g, '');
  if (digits.length !== 14) return null;
  const century = digits[0] === '2' ? 1900 : digits[0] === '3' ? 2000 : null;
  if (century === null) return null;
  const year = century + Number(digits.slice(1, 3));
  const month = Number(digits.slice(3, 5));
  const day = Number(digits.slice(5, 7));
  const birthDate = new Date(year, month - 1, day);
  if (birthDate.getFullYear() !== year || birthDate.getMonth() !== month - 1 || birthDate.getDate() !== day) return null;
  if (birthDate > today) return null;
  const governorateCode = digits.slice(7, 9);
  const governorate = governorates[governorateCode];
  if (!governorate) return null;
  const genderDigit = Number(digits[12]);
  if (Number.isNaN(genderDigit)) return null;
  let age = today.getFullYear() - year;
  const hadBirthday = today.getMonth() > birthDate.getMonth() || (today.getMonth() === birthDate.getMonth() && today.getDate() >= birthDate.getDate());
  if (!hadBirthday) age -= 1;
  return {
    birthDate,
    gender: genderDigit % 2 === 1 ? 'male' : 'female',
    governorateCode,
    governorateAr: governorate[0],
    governorateEn: governorate[1],
    age,
  };
}
