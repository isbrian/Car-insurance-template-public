export const vehicleCategoryOptions = [
  '自用小客車',
  '自用小貨車',
  '客貨兩用車',
  '營業用小客車',
  '普通重型機車',
].map((value) => ({ label: value, value }))

export const thirdPartyOptions = [
  { label: '不投保', value: 'none' },
  { label: '體傷 100 萬／財損 30 萬', value: '1000000|300000' },
  { label: '體傷 200 萬／財損 50 萬', value: '2000000|500000' },
  { label: '體傷 300 萬／財損 100 萬', value: '3000000|1000000' },
  { label: '體傷 500 萬／財損 100 萬', value: '5000000|1000000' },
]

export const excessLiabilityOptions = [
  { label: '不投保', value: 'none' },
  { label: '500 萬', value: '5000000' },
  { label: '1,000 萬', value: '10000000' },
  { label: '2,000 萬', value: '20000000' },
]

export const ownDamageOptions = [
  { label: '不投保', value: 'none' },
  { label: '甲式', value: '甲式' },
  { label: '乙式', value: '乙式' },
  { label: '丙式', value: '丙式' },
  { label: '限額車對車', value: '限額車對車' },
]

export const deductibleOptions = [0, 3000, 5000, 10000, 20000].map((value) => ({
  label: value === 0 ? '無自負額' : `${value.toLocaleString('zh-TW')} 元`,
  value,
}))

export const driverAccidentOptions = [
  { label: '不投保', value: 'none' },
  { label: '100 萬', value: '1000000' },
  { label: '200 萬', value: '2000000' },
  { label: '300 萬', value: '3000000' },
]

export const riderOptions = [
  { label: '天災（颱風地震洪水）', value: '天災(颱風地震洪水)' },
  { label: '零件裝備被竊', value: '零件裝備被竊' },
  { label: '代步車費用', value: '代步車費用' },
]

export const channelOptions = [
  { label: '業務櫃檯', value: '業務櫃檯' },
  { label: '電話', value: '電話' },
  { label: '網路', value: '網路' },
  { label: '合作通路', value: '合作通路' },
]
