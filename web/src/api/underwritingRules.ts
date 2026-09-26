import { requestJson } from './http'

export type UnderwritingRule = {
  id: number
  code: string
  name: string
  conditionType: string
  conditionJson: string
  action: string
  message: string
  isEnabled: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export function getUnderwritingRules(signal?: AbortSignal): Promise<UnderwritingRule[]> {
  return requestJson<UnderwritingRule[]>('/api/underwriting-rules', { signal }, true)
}

export function updateUnderwritingRuleEnabled(
  rule: UnderwritingRule,
  isEnabled: boolean,
): Promise<UnderwritingRule> {
  return requestJson<UnderwritingRule>(`/api/underwriting-rules/${rule.id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      code: rule.code,
      name: rule.name,
      conditionType: rule.conditionType,
      conditionJson: rule.conditionJson,
      action: rule.action,
      message: rule.message,
      isEnabled,
    }),
  }, true)
}
