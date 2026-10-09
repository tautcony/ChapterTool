## MODIFIED Requirements

### Requirement: Global styles preserve workflow usability
The ported style layer SHALL keep ChapterTool workflows usable at supported window sizes. Preview readiness, diagnostics, and inline comparison SHALL NOT add rows to the bottom options form or change the allocated chapter viewport bounds at a fixed window size and input layout. Narrow comparison cells MAY use two labeled lines without reducing the allocated viewport.

#### Scenario: Main window uses the global styles
- **WHEN** the main window opens
- **THEN** the load and save area, chapter grid, options area, and status strip SHALL remain available
- **AND** controls SHALL not overlap

#### Scenario: Tool window uses a narrow supported size
- **WHEN** a tool window is resized to its minimum width
- **THEN** primary actions SHALL remain visible
- **AND** text SHALL remain inside its control bounds

#### Scenario: Inline comparison preserves chapter space
- **WHEN** a fixed-size main window moves through empty, computing, ready, invalid, unchanged, and stale preview states
- **THEN** its allocated chapter viewport bounds SHALL remain equal within layout rounding tolerance
- **AND** no detailed result panel SHALL appear below the options form
- **AND** responsive comparison SHALL remain readable without overlapping values

## ADDED Requirements

### Requirement: Candidate comparisons appear in original chapter cells
候选预览 MUST 显示在原章节表的对应单元格内。原值和预览值 MUST 明确区分。时间变化 MUST 显示带符号的变化量。宽窗口 MUST 使用可读的同格横向比较；窄窗口 MUST 使用紧凑两行比较或表格内滚动。单元格 MUST NOT 显示“原/预览”或 `Original/Preview` 文本标签。图标、语义颜色、无障碍名称及工具提示 MUST 区分已提交值和预览值。普通逐章比较 MUST NOT 要求打开独立审阅弹框。

#### Scenario: Review a time shift in place
- **WHEN** 用户准备 `t + 1` 候选
- **THEN** 对应章节的时间单元格显示原时间、预览时间和正一秒变化量
- **AND** 章节名称和其他未变化字段继续显示已提交值
- **AND** 短表头保持时间、名称、帧和编号
- **AND** 图标和语义颜色明确表示尚未应用的预览，辅助功能名称或工具提示解释值的角色
- **AND** 用户沿原章节位置即可比对结果

#### Scenario: Compare other fields
- **WHEN** 候选改变名称、编号或帧信息
- **THEN** 对应原单元格显示实际原值和目标值
- **AND** 未变化单元格不出现虚假的变化标记
- **AND** 显示的原帧值与进入预览前同一行的可见帧值一致
- **AND** 只有进入预览前没有可见帧计算值时才显示本地化缺失值
- **AND** 原帧保留进入预览前可见行的准确、近似或中性颜色
- **AND** 候选帧按候选准确度显示对应语义颜色，不使用统一的预览强调色
- **AND** 文档或轨道属性差异可从相关现有控件的本地化工具提示检查
- **AND** 缺失值与零值使用不同业务表示

### Requirement: Inline review preserves row identity and committed values
比较 MUST 通过稳定轨道及章节身份映射。原行顺序、未变化章节、选择、滚动和虚拟化 MUST 保持。准备和展示 MUST NOT 修改已提交字段或产生内容事务。

#### Scenario: Compare repeated names and shifted numbering
- **WHEN** 多个章节有相同名称且候选改变编号
- **THEN** 对比通过稳定身份显示在正确原行
- **AND** 不按名称、显示编号或行索引推断对应关系

#### Scenario: Review a large candidate
- **WHEN** 候选涉及 1,000 个章节并且用户滚动到末行
- **THEN** 所有原章节仍然可达且比较值正确
- **AND** 表格不默认只显示变化行或插入重复章节行
- **AND** 可视区域分配高度不因候选大小变化

#### Scenario: Save while preview is visible
- **WHEN** 用户尚未确认候选而保存已提交内容
- **THEN** 保存结果不包含未应用的预览值
- **AND** 展示投影不改写正常编辑字段或导出源

### Requirement: Grid preview is read-only while candidate input remains editable
有效候选展示期间章节表及直接内容变更入口 MUST 只读。滚动和选择 MUST 继续可用。表达式输入 MUST 保持可编辑。输入或基础内容变化 MUST 立即使旧候选不可应用。

#### Scenario: Edit an expression during review
- **WHEN** 用户在预览状态修改表达式
- **THEN** 旧候选立即失效且应用禁用
- **AND** 旧目标值清除或明确标为过期，不能冒充当前结果
- **AND** 自动准备完成后仅展示最新草稿对应候选
- **AND** 输入编辑的撤销不改变文档历史

#### Scenario: Attempt a chapter edit during preview
- **WHEN** 用户尝试编辑单元格、插入或删除章节
- **THEN** 直接内容编辑保持禁用，并明确提供放弃预览的动作
- **AND** 放弃后普通表格编辑恢复
- **AND** 预览准备前的单元格草稿按既有规则结束

### Requirement: Inline preview actions share the existing options row
主窗口 MUST 在原操作槽显示应用及放弃操作，并保留需要的更新和错误反馈。它 MUST NOT 新增确认行或结果面板。章节摘要和重复逐章详情入口 MUST NOT 显示。无法放入章节单元格的属性差异 MUST 通过相关现有操作的本地化工具提示检查。错误 MUST 使用现有错误反馈显示。

#### Scenario: Confirm an inline result
- **WHEN** 有效当前候选显示在原章节表中
- **THEN** 原操作槽提供应用和放弃按钮
- **AND** 原操作槽不显示章节摘要、完整范围摘要工具提示或重复逐章详情按钮
- **AND** 无需先打开模态审阅才能应用
- **AND** 入口标明实际操作而不把所有候选都误标为表达式

#### Scenario: Review property-only effects
- **WHEN** 候选只有文档或轨道属性变化
- **THEN** 相关现有操作的本地化工具提示显示属性差异
- **AND** 用户可查看原值、目标值和所属范围
- **AND** 即使章节时间不变，完整候选有变化时仍可应用
- **AND** 检查工具提示不增加底部布局行或修改候选

#### Scenario: Invalid, unchanged, or stale preview
- **WHEN** 候选无效、无变化、计算中或过期
- **THEN** 应用禁用且原操作槽显示对应状态
- **AND** 错误通过现有错误反馈查看
- **AND** 更新过期结果需要显式动作，不能替换后自动提交

### Requirement: Apply and discard restore the normal table
应用 MUST 提交原表格实际展示的有效当前候选，不得重新计算表达式。放弃 MUST 移除比较并保持原文档。成功或放弃后 MUST 恢复普通编辑且尽可能保留选择和滚动。

#### Scenario: Apply and undo
- **WHEN** 用户点击应用
- **THEN** 目标值成为普通表格的已提交值且比较标记消失
- **AND** 只创建一次原子事务，一次撤销恢复全部变化
- **AND** 重复点击不重复提交

#### Scenario: Discard without erasing input
- **WHEN** 用户点击放弃
- **THEN** 原值恢复为普通表格，文档和历史保持不变
- **AND** 表达式草稿保留
- **AND** 同一草稿版本不立即自动重建被放弃候选
- **AND** 下一次修改或显式准备可以创建新候选

### Requirement: Bottom option inputs share responsive label alignment
底部选项中的文本输入 MUST 使用响应式共享标签列，且不能按某个固定语言文本设置像素宽度。表达式编辑器 MUST 与同列输入左边界对齐，并在中、英、日文、字号变化及宽窄布局中保持一致。

#### Scenario: Align bottom text inputs
- **WHEN** 主窗口以支持语言和字体大小渲染
- **THEN** 保存格式、XML 语言及表达式输入的同列控件左边界对齐
- **AND** 断言实际 TextBox 或 ExpressionEditor 的边界，而不是只检查容器

### Requirement: Inline review requires rendered workflow evidence
验收 MUST 覆盖中文及英文、760×600、1280×800、760×520 和布局阈值两侧。测试 MUST 检查原值保持、身份映射、单元格可读性及表格可视区域。截图 MUST 补充行为断言。

#### Scenario: Validate the inline design
- **WHEN** 对本 change 执行验收
- **THEN** 验证预览前后可视区域边界相同，允许窄布局两行单元格增加行高
- **AND** 验证显示帧率 24000/1001 下原 0 和 8357 等可见帧值在预览中保持，且候选目标帧正确
- **AND** 验证无候选时没有可见原/预标签、章节摘要或重复详情按钮
- **AND** 验证箭头图标资源、颜色、accessible name 及工具提示
- **AND** 验证稳定身份、未变化行、末行、名称及帧变化、属性独立变化和亚毫秒时间差
- **AND** 验证应用、放弃、候选失效和一次撤销
- **AND** 代表性字体放大及深浅主题保持可用
- **AND** 截图保存在 `artifacts/compact-content-preview-review/`
