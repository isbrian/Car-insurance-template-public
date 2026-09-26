import { LockOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login, verifyMfa } from '../../api/auth'
import type { MfaChallenge } from '../../api/auth'
import { ApiError } from '../../api/http'
import { useAuth } from '../../auth/AuthContext'

type LoginFormValues = {
  account: string
  password: string
}

type MfaFormValues = {
  code: string
}

function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return '登入服務暫時無法使用，請稍後再試。'
}

function LoginPage() {
  const navigate = useNavigate()
  const { completeLogin } = useAuth()
  const [challenge, setChallenge] = useState<MfaChallenge | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const handleLogin = async (values: LoginFormValues) => {
    setSubmitting(true)
    setErrorMessage(null)

    try {
      const result = await login({
        account: values.account.trim(),
        password: values.password,
        rememberDevice: false,
      })
      setChallenge(result)
    } catch (error) {
      setErrorMessage(getErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  const handleMfa = async (values: MfaFormValues) => {
    if (!challenge) return

    setSubmitting(true)
    setErrorMessage(null)

    try {
      const result = await verifyMfa({
        mfaToken: challenge.mfaToken,
        code: values.code.trim(),
      })
      completeLogin(result.accessToken, result.user)
      navigate('/dashboard', { replace: true })
    } catch (error) {
      setErrorMessage(getErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  const resetChallenge = () => {
    setChallenge(null)
    setErrorMessage(null)
  }

  return (
    <main className="login-shell">
      <Card className="login-card" bordered={false}>
        <Space direction="vertical" size={6} className="login-heading">
          <Typography.Title level={2}>車險作業平台</Typography.Title>
          <Typography.Text type="secondary">
            {challenge ? '完成二階段驗證後即可進入系統。' : '請使用內部帳號登入。'}
          </Typography.Text>
        </Space>

        {errorMessage && (
          <Alert
            className="login-alert"
            type="error"
            showIcon
            message="驗證失敗"
            description={errorMessage}
          />
        )}

        {!challenge ? (
          <Form<LoginFormValues>
            layout="vertical"
            onFinish={handleLogin}
            requiredMark={false}
            autoComplete="on"
          >
            <Form.Item
              label="帳號或 Email"
              name="account"
              rules={[{ required: true, message: '請輸入帳號或 Email' }]}
            >
              <Input prefix={<UserOutlined />} autoComplete="username" placeholder="例如：admin" />
            </Form.Item>
            <Form.Item
              label="密碼"
              name="password"
              rules={[{ required: true, message: '請輸入密碼' }]}
            >
              <Input.Password prefix={<LockOutlined />} autoComplete="current-password" placeholder="請輸入密碼" />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={submitting} block size="large">
              登入
            </Button>
          </Form>
        ) : (
          <Form<MfaFormValues>
            layout="vertical"
            onFinish={handleMfa}
            requiredMark={false}
            autoComplete="off"
          >
            <Alert
              className="login-alert"
              type="info"
              showIcon
              message={`驗證碼已送至 ${challenge.maskedPhone}`}
              description="Demo 環境固定驗證碼為 123456；此設定僅供展示，不代表正式環境安全設計。"
            />
            <Form.Item
              label="MFA 驗證碼"
              name="code"
              rules={[
                { required: true, message: '請輸入驗證碼' },
                { len: 6, message: '驗證碼需為 6 碼' },
                { pattern: /^\d{6}$/, message: '驗證碼只能包含數字' },
              ]}
            >
              <Input
                prefix={<SafetyCertificateOutlined />}
                inputMode="numeric"
                maxLength={6}
                placeholder="123456"
              />
            </Form.Item>
            <Space direction="vertical" size={12} className="login-actions">
              <Button type="primary" htmlType="submit" loading={submitting} block size="large">
                驗證並進入系統
              </Button>
              <Button type="link" onClick={resetChallenge} disabled={submitting} block>
                返回帳密登入
              </Button>
            </Space>
          </Form>
        )}
      </Card>
    </main>
  )
}

export default LoginPage
